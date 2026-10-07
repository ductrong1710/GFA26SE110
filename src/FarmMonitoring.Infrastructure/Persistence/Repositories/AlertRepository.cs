using FarmMonitoring.Application.Features.Farms;
using System.Linq.Expressions;
using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Alerts;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Constants;
using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FarmMonitoring.Infrastructure.Persistence.Repositories;

public sealed class AlertRepository(AppDbContext db) : IAlertRepository
{
    public async Task<T> InTransactionAsync<T>(Func<Task<T>> action, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var result = await action();
        await transaction.CommitAsync(ct);
        return result;
    }
    private static int SubjectId(AlertSignal s) => s.Type switch
    {
        AlertType.SENSOR_THRESHOLD or AlertType.SENSOR_DATA_TIMEOUT => s.SensorChannelId ?? throw new InvalidOperationException("Sensor channel required."),
        AlertType.SENSOR_LOW_BATTERY => s.SensorNodeId ?? throw new InvalidOperationException("Sensor node required."),
        AlertType.GATEWAY_ERROR or AlertType.GATEWAY_OFFLINE => s.GatewayId ?? throw new InvalidOperationException("Gateway required."),
        AlertType.UAV_LOW_BATTERY => s.UavId ?? throw new InvalidOperationException("UAV required."),
        _ => s.MissionId ?? throw new InvalidOperationException("Mission required.")
    };
    private static Expression<Func<Alert, bool>> Subject(AlertSignal s)
    {
        var id = SubjectId(s);
        return s.Type switch
        {
            AlertType.SENSOR_THRESHOLD or AlertType.SENSOR_DATA_TIMEOUT => x => x.SensorChannelId == id,
            AlertType.SENSOR_LOW_BATTERY => x => x.SensorNodeId == id,
            AlertType.GATEWAY_ERROR or AlertType.GATEWAY_OFFLINE => x => x.GatewayId == id,
            AlertType.UAV_LOW_BATTERY => x => x.UavId == id,
            _ => x => x.MissionId == id
        };
    }
    public async Task LockSubjectAsync(AlertSignal signal, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Alert generation requires a transaction.");
        var key = $"alert:{signal.Type}:{SubjectId(signal)}";
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", ct);
    }
    public async Task<Alert?> FindActiveAsync(AlertSignal signal, CancellationToken ct)
    {
        var predicate = Subject(signal);
        return db.Alerts.Local.Where(x => db.Entry(x).State == EntityState.Added && x.AlertType == signal.Type && x.Status != AlertStatus.CLOSED).FirstOrDefault(predicate.Compile())
            ?? await db.Alerts.AsNoTracking().Where(x => x.AlertType == signal.Type && x.Status != AlertStatus.CLOSED).Where(predicate).FirstOrDefaultAsync(ct);
    }
    public async Task<Alert?> FindAsync(int id, bool forUpdate, CancellationToken ct)
    {
        if (forUpdate && await db.Alerts.FromSqlInterpolated($"SELECT * FROM alerts WHERE id = {id} FOR UPDATE").SingleOrDefaultAsync(ct) is null) return null;
        var rows = forUpdate ? db.Alerts.AsQueryable() : db.Alerts.AsNoTracking();
        return await rows.Include(x => x.History).SingleOrDefaultAsync(x => x.Id == id, ct);
    }
    public Task<SensorThreshold?> ThresholdAsync(int channelId, CancellationToken ct) => db.SensorThresholds
        .Include(x => x.SensorChannel).ThenInclude(x => x.SensorNode).SingleOrDefaultAsync(x => x.SensorChannelId == channelId, ct);
    public async Task<int[]> RecipientsAsync(AlertSignal signal, CancellationToken ct)
    {
        int? farmId = signal.SensorChannelId.HasValue
            ? await db.SensorChannels.Where(x => x.Id == signal.SensorChannelId).Select(x => (int?)x.SensorNode.Zone.FarmId).SingleOrDefaultAsync(ct)
            : signal.SensorNodeId.HasValue
                ? await db.SensorNodes.Where(x => x.Id == signal.SensorNodeId).Select(x => (int?)x.Zone.FarmId).SingleOrDefaultAsync(ct)
                : await db.Missions.Where(x => x.Id == signal.MissionId).Select(x => (int?)x.FarmId).SingleOrDefaultAsync(ct);
        return await db.Users.AsNoTracking().Where(x => x.IsActive &&
            (x.UserRoles.Any(r => r.Role.Name == RoleNames.FarmAdministrator) ||
                (farmId.HasValue && x.UserFarms.Any(f => f.FarmId == farmId) &&
                    x.UserRoles.Any(r => r.Role.Name == RoleNames.FarmOwner || r.Role.Name == RoleNames.FarmEngineer))))
            .Select(x => x.Id).ToArrayAsync(ct);
    }
    public void Add(Alert alert) => db.Alerts.Add(alert);
    public void AddNotification(Notification notification) => db.Notifications.Add(notification);
    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
    public async Task<PagedResult<AlertResponse>> ListAsync(AlertQuery query, FarmAccessScope scope, CancellationToken ct)
    {
        var rows = db.Alerts.AsNoTracking().ForFarms(db, scope, FarmAccessQuery.AlertFarm);
        if (query.Status is not null) { var value = Enum.Parse<AlertStatus>(query.Status); rows = rows.Where(x => x.Status == value); }
        if (query.Severity is not null) { var value = Enum.Parse<AlertSeverity>(query.Severity); rows = rows.Where(x => x.Severity == value); }
        if (query.AlertType is not null) { var value = Enum.Parse<AlertType>(query.AlertType); rows = rows.Where(x => x.AlertType == value); }
        if (query.SensorNodeId.HasValue) rows = rows.Where(x => x.SensorNodeId == query.SensorNodeId);
        if (query.GatewayId.HasValue) rows = rows.Where(x => x.GatewayId == query.GatewayId);
        if (query.MissionId.HasValue) rows = rows.Where(x => x.MissionId == query.MissionId);
        if (query.FarmId.HasValue) rows = rows.Where(x => (x.SensorChannel != null ? (int?)x.SensorChannel.SensorNode.Zone.FarmId :
            x.SensorNode != null ? x.SensorNode.Zone.FarmId : x.Mission != null ? x.Mission.FarmId : null) == query.FarmId);
        if (query.ZoneId.HasValue) rows = rows.Where(x => x.SensorNode != null && x.SensorNode.ZoneId == query.ZoneId);
        if (query.From.HasValue) { var from = query.From.Value.ToUniversalTime(); rows = rows.Where(x => x.OpenedAt >= from); }
        if (query.To.HasValue) { var to = query.To.Value.ToUniversalTime(); rows = rows.Where(x => x.OpenedAt <= to); }
        if (!string.IsNullOrWhiteSpace(query.Search)) rows = rows.Where(x => x.Message.Contains(query.Search.Trim()));
        var count = await rows.CountAsync(ct);
        var items = await rows.OrderByDescending(x => x.OpenedAt).ThenByDescending(x => x.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new AlertResponse(x.Id, x.AlertType.ToString(), x.Severity.ToString(), x.Status.ToString(), x.SensorNodeId, x.SensorChannelId, x.GatewayId,
                x.UavId, x.MissionId, x.Message, x.TriggeredValue, x.OpenedAt, x.AcknowledgedAt, x.ClosedAt, null)).ToArrayAsync(ct);
        return new(items, count, query.Page, query.PageSize);
    }
    public async Task<PagedResult<NotificationResponse>> NotificationsAsync(int userId, NotificationQuery query, CancellationToken ct)
    {
        var rows = db.Notifications.AsNoTracking().Where(x => x.UserId == userId && x.Channel == NotificationChannel.WEB);
        if (query.AlertId.HasValue) rows = rows.Where(x => x.AlertId == query.AlertId);
        if (!string.IsNullOrWhiteSpace(query.Search)) rows = rows.Where(x => x.Message.Contains(query.Search.Trim()));
        var count = await rows.CountAsync(ct);
        var items = await rows.OrderByDescending(x => x.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new NotificationResponse(x.Id, x.AlertId, x.Channel.ToString(), x.Subject, x.Message, x.Status.ToString(), x.SentAt, x.CreatedAt)).ToArrayAsync(ct);
        return new(items, count, query.Page, query.PageSize);
    }
    public async Task<Notification?> ReadNotificationAsync(int userId, int id, CancellationToken ct)
    {
        var rows = db.Notifications.Where(x => x.UserId == userId && x.Id == id && x.Channel == NotificationChannel.WEB);
        await rows.ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Status, NotificationStatus.READ), ct);
        return await rows.AsNoTracking().SingleOrDefaultAsync(ct);
    }
}
