using FarmMonitoring.Application.Features.Farms;
using FarmMonitoring.Application.Features.Equipment;
using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Reports;
using FarmMonitoring.Application.Features.Rentals;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FarmMonitoring.Infrastructure.Persistence.Repositories;

public sealed class ReportRepository(AppDbContext db, TimeProvider clock, RentalExpirationSettings rentalSettings) : IReportRepository
{
    private IQueryable<Alert> AlertsInFarm(IQueryable<Alert> rows, int farmId) =>
        rows.Where(x => (x.SensorChannel != null ? (int?)x.SensorChannel.SensorNode.Zone.FarmId :
            x.SensorNode != null ? x.SensorNode.Zone.FarmId : x.Mission != null ? x.Mission.FarmId : null) == farmId);
    public async Task<DashboardOverview> DashboardAsync(FarmAccessScope scope, int? farmId, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
        var farmRows = db.Farms.AsNoTracking().ForFarms(db, scope, x => x.Id);
        if (farmId.HasValue) farmRows = farmRows.Where(x => x.Id == farmId);
        var farmIds = farmRows.Select(x => x.Id);
        var missionRows = FarmAccessQuery.VisibleMissions(db, scope).Where(x => farmIds.Contains(x.FarmId));
        var alertRows = db.Alerts.AsNoTracking().ForFarms(db, scope, FarmAccessQuery.AlertFarm);
        if (farmId.HasValue) alertRows = AlertsInFarm(alertRows, farmId.Value);
        var gatewayRows = db.Gateways.AsNoTracking();
        var rentals = FarmAccessQuery.RentalAssignments(db, scope).Where(x => farmIds.Contains(x.Contract.FarmId));
        if (!scope.IsAdministrator || farmId.HasValue) gatewayRows = gatewayRows.Where(x => rentals.Any(r => r.GatewayId == x.Id));
        var farms = await farmRows.CountAsync(ct);
        var sensors = await db.SensorNodes.Where(x => farmIds.Contains(x.Zone.FarmId)).GroupBy(x => 1).Select(g => new DeviceCounts(g.Count(),
            g.Count(x => x.IsActive && x.Status.ToUpper() == "ONLINE"), g.Count(x => x.IsActive && x.Status.ToUpper() == "OFFLINE"),
            g.Count(x => x.IsActive && x.Status.ToUpper() != "ONLINE" && x.Status.ToUpper() != "OFFLINE"), g.Count(x => !x.IsActive)))
            .SingleOrDefaultAsync(ct) ?? new(0, 0, 0, 0, 0);
        var now = clock.GetUtcNow();
        var cutoff = now.AddSeconds(-GatewayPresence.TimeoutSeconds);
        var gateways = await gatewayRows.GroupBy(x => 1).Select(g => new DeviceCounts(g.Count(),
            g.Count(x => x.IsActive && x.LastSeenAt >= cutoff && x.LastSeenAt <= now),
            g.Count(x => x.IsActive && (x.LastSeenAt == null || x.LastSeenAt < cutoff || x.LastSeenAt > now)),
            0, g.Count(x => !x.IsActive)))
            .SingleOrDefaultAsync(ct) ?? new(0, 0, 0, 0, 0);
        var missions = await missionRows.GroupBy(x => 1).Select(g => new MissionCounts(g.Count(x => x.Status == MissionStatus.SCHEDULED),
            g.Count(x => x.Status == MissionStatus.RUNNING), g.Count(x => x.Status == MissionStatus.FAILED))).SingleOrDefaultAsync(ct) ?? new(0, 0, 0);
        var alerts = await alertRows.GroupBy(x => 1).Select(g => new AlertCounts(g.Count(x => x.Status == AlertStatus.OPEN),
            g.Count(x => x.Status != AlertStatus.CLOSED && x.Severity == AlertSeverity.CRITICAL), g.Count(x => x.Status == AlertStatus.ACKNOWLEDGED))).SingleOrDefaultAsync(ct) ?? new(0, 0, 0);
        var operations = await OperationsAsync(scope, farmIds, now, ct);
        var history = scope.IsAdministrator ? await AdminHistoryAsync(farmIds, alertRows, missionRows, now, ct) : null;
        await transaction.CommitAsync(ct);
        return new(farms, sensors, gateways, missions, alerts, operations, history);
    }
    private async Task<DashboardHistoryRow[]> AdminHistoryAsync(IQueryable<int> farms, IQueryable<Alert> alerts,
        IQueryable<Mission> missions, DateTimeOffset now, CancellationToken ct)
    {
        var from = new DateTimeOffset(now.UtcDateTime.Date.AddDays(-29), TimeSpan.Zero);
        var rows = await alerts.Where(x => x.OpenedAt >= from && x.OpenedAt <= now)
            .GroupBy(x => DateOnly.FromDateTime(x.OpenedAt.UtcDateTime))
            .Select(g => new DashboardHistoryRow(g.Key, "ALERT_OPENED", g.Count())).ToListAsync(ct);
        rows.AddRange(await missions.Where(x => x.CompletedAt >= from && x.CompletedAt <= now &&
                (x.Status == MissionStatus.COMPLETED || x.Status == MissionStatus.FAILED || x.Status == MissionStatus.CANCELLED))
            .GroupBy(x => new { Date = DateOnly.FromDateTime(x.CompletedAt!.Value.UtcDateTime), x.Status })
            .Select(g => new DashboardHistoryRow(g.Key.Date, "MISSION_" + g.Key.Status.ToString(), g.Count())).ToListAsync(ct));
        rows.AddRange(await db.Set<InstallationJobHistory>().Where(x => farms.Contains(x.Job.InstallationRequest.FarmId) &&
                x.CreatedAt >= from && x.CreatedAt <= now)
            .GroupBy(x => new { Date = DateOnly.FromDateTime(x.CreatedAt.UtcDateTime), x.Action })
            .Select(g => new DashboardHistoryRow(g.Key.Date, "INSTALLATION_" + g.Key.Action.ToString(), g.Count())).ToListAsync(ct));
        rows.AddRange(await db.Set<ServiceJobHistory>().Where(x => farms.Contains(x.Job.RentalDeviceAssignment.Contract.FarmId) &&
                x.CreatedAt >= from && x.CreatedAt <= now)
            .GroupBy(x => new { Date = DateOnly.FromDateTime(x.CreatedAt.UtcDateTime), x.Job.Type, x.Action })
            .Select(g => new DashboardHistoryRow(g.Key.Date, g.Key.Type.ToString() + "_" + g.Key.Action.ToString(), g.Count())).ToListAsync(ct));
        return rows.OrderBy(x => x.Date).ThenBy(x => x.Category).ToArray();
    }
    private async Task<DashboardOperations> OperationsAsync(FarmAccessScope scope, IQueryable<int> farmIds, DateTimeOffset now, CancellationToken ct)
    {
        var contracts = db.RentalContracts.Where(x => farmIds.Contains(x.FarmId) && (scope.IsAdministrator || x.OwnerId == scope.UserId));
        var active = contracts.Where(x => x.Status == RentalStatus.ACTIVE && x.StartAt <= now && x.EndAt > now);
        var warningAt = now.AddDays(rentalSettings.WarningDays);
        var installations = await db.InstallationJobs.CountAsync(x => farmIds.Contains(x.InstallationRequest.FarmId) &&
            (scope.IsAdministrator || x.InstallationRequest.OwnerId == scope.UserId) && x.Status != InstallationJobStatus.APPROVED, ct);
        var serviceCounts = await db.ServiceJobs.Where(x => contracts.Any(c => c.Id == x.RentalDeviceAssignment.ContractId) && x.Status != ServiceJobStatus.CLOSED)
            .GroupBy(x => x.Type).Select(g => new { Type = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Type, x => x.Count, ct);
        var seasons = db.CropSeasons.Where(x => farmIds.Contains(x.FarmId) && (scope.IsAdministrator || x.OwnerId == scope.UserId));
        var recent = await db.CultivationActivities.Where(x => seasons.Any(s => s.Id == x.SeasonId))
            .OrderByDescending(x => x.Date).ThenByDescending(x => x.Id).Take(5)
            .Select(x => new RecentCultivation(x.Id, x.SeasonId, x.Season.Name, x.Date, x.Type.ToString(), x.Description)).ToArrayAsync(ct);
        return new(installations, serviceCounts.GetValueOrDefault(ServiceJobType.MAINTENANCE), serviceCounts.GetValueOrDefault(ServiceJobType.REPAIR),
            await active.CountAsync(ct), await active.CountAsync(x => x.EndAt <= warningAt, ct),
            await contracts.CountAsync(x => x.Status == RentalStatus.EXPIRED || x.Status == RentalStatus.ACTIVE && x.EndAt <= now, ct),
            rentalSettings.WarningDays, await seasons.CountAsync(x => x.Status == CropSeasonStatus.ACTIVE, ct), recent);
    }
    public async Task<PagedResult<SensorReportRow>> SensorsAsync(ReportQuery query, ReportPeriod period, FarmAccessScope scope, CancellationToken ct)
    {
        var rows = db.SensorReadings.AsNoTracking().ForFarms(db, scope, x => x.SensorChannel.SensorNode.Zone.FarmId).Where(x => x.IsValid && x.MeasuredAt >= period.From && x.MeasuredAt <= period.To);
        if (query.FarmId.HasValue) rows = rows.Where(x => x.SensorChannel.SensorNode.Zone.FarmId == query.FarmId);
        if (query.ZoneId.HasValue) rows = rows.Where(x => x.SensorChannel.SensorNode.ZoneId == query.ZoneId);
        if (query.SensorTypeId.HasValue) rows = rows.Where(x => x.SensorChannel.SensorTypeId == query.SensorTypeId);
        if (!string.IsNullOrWhiteSpace(query.Search)) rows = rows.Where(x => x.SensorChannel.ChannelCode.Contains(query.Search.Trim()) || x.SensorChannel.SensorNode.Name.Contains(query.Search.Trim()));
        var groups = rows.GroupBy(x => new { x.SensorChannelId, x.SensorChannel.SensorNodeId, x.SensorChannel.ChannelCode, x.SensorChannel.SensorType.Unit, x.SensorChannel.SensorNode.ZoneId });
        var count = await groups.CountAsync(ct);
        var items = await groups.OrderBy(g => g.Key.SensorChannelId).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(g => new SensorReportRow(g.Key.SensorChannelId, g.Key.SensorNodeId, g.Key.ChannelCode, g.Key.Unit, g.Key.ZoneId,
                g.LongCount(), g.Min(x => x.Value), g.Max(x => x.Value), g.Average(x => x.Value), g.Min(x => x.MeasuredAt), g.Max(x => x.MeasuredAt))).ToArrayAsync(ct);
        return new(items, count, query.Page, query.PageSize);
    }
    public async Task<PagedResult<MissionReportRow>> MissionsAsync(ReportQuery query, ReportPeriod period, FarmAccessScope scope, CancellationToken ct)
    {
        var rows = FarmAccessQuery.VisibleMissions(db, scope).Where(x => x.CreatedAt >= period.From && x.CreatedAt <= period.To);
        if (query.FarmId.HasValue) rows = rows.Where(x => x.FarmId == query.FarmId);
        if (!string.IsNullOrWhiteSpace(query.Search)) rows = rows.Where(x => x.Name.Contains(query.Search.Trim()));
        var count = await rows.CountAsync(ct);
        var items = await rows.OrderByDescending(x => x.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new MissionReportRow(x.Id, x.Name, x.FarmId, x.Status.ToString(), x.CreatedAt, x.StartedAt, x.CompletedAt, x.Targets.Count,
                x.Targets.Count(t => t.Status == MissionTargetStatus.COLLECTED), x.Targets.Count(t => t.Status == MissionTargetStatus.FAILED), x.Targets.Count(t => t.Status == MissionTargetStatus.SKIPPED))).ToArrayAsync(ct);
        return new(items, count, query.Page, query.PageSize);
    }
    public async Task<PagedResult<AlertReportRow>> AlertsAsync(ReportQuery query, ReportPeriod period, FarmAccessScope scope, CancellationToken ct)
    {
        var rows = db.Alerts.AsNoTracking().ForFarms(db, scope, FarmAccessQuery.AlertFarm).Where(x => x.OpenedAt >= period.From && x.OpenedAt <= period.To);
        if (query.FarmId.HasValue) rows = AlertsInFarm(rows, query.FarmId.Value);
        if (query.ZoneId.HasValue) rows = rows.Where(x => x.SensorNode != null && x.SensorNode.ZoneId == query.ZoneId);
        if (!string.IsNullOrWhiteSpace(query.Search)) rows = rows.Where(x => x.Message.Contains(query.Search.Trim()));
        var groups = rows.GroupBy(x => new { x.AlertType, x.Severity, x.Status });
        var count = await groups.CountAsync(ct);
        var items = await groups.OrderBy(g => g.Key.AlertType).ThenBy(g => g.Key.Severity).ThenBy(g => g.Key.Status)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).Select(g => new AlertReportRow(g.Key.AlertType.ToString(),
                g.Key.Severity.ToString(), g.Key.Status.ToString(), g.LongCount(), g.Min(x => x.OpenedAt), g.Max(x => x.OpenedAt))).ToArrayAsync(ct);
        return new(items, count, query.Page, query.PageSize);
    }
    public async Task<PagedResult<DeviceReportRow>> DevicesAsync(DeviceReportQuery query, FarmAccessScope scope, CancellationToken ct)
    {
        var sensorRows = db.SensorNodes.AsNoTracking().ForFarms(db, scope, x => x.Zone.FarmId);
        var rentals = FarmAccessQuery.RentalAssignments(db, scope);
        if (query.FarmId.HasValue)
        {
            sensorRows = sensorRows.Where(x => x.Zone.FarmId == query.FarmId);
            rentals = rentals.Where(x => x.Contract.FarmId == query.FarmId);
        }
        var uavRows = db.Uavs.AsNoTracking();
        var gatewayRows = db.Gateways.AsNoTracking();
        if (!scope.IsAdministrator || query.FarmId.HasValue)
        {
            uavRows = uavRows.Where(x => rentals.Any(r => r.UavId == x.Id));
            gatewayRows = gatewayRows.Where(x => rentals.Any(r => r.GatewayId == x.Id));
        }
        var sensors = sensorRows.Select(x => new { DeviceType = "SENSOR_NODE", x.Id, Code = x.DeviceCode, x.Name, x.Status, x.IsActive, x.BatteryPercent, x.LastSeenAt });
        var uavs = uavRows.Select(x => new { DeviceType = "UAV", x.Id, x.Code, x.Name, x.Status, x.IsActive, x.BatteryPercent, x.LastSeenAt });
        var now = clock.GetUtcNow();
        var cutoff = now.AddSeconds(-GatewayPresence.TimeoutSeconds);
        var gateways = gatewayRows.Select(x => new { DeviceType = "GATEWAY", x.Id, x.Code, x.Name,
            Status = x.IsActive && x.LastSeenAt >= cutoff && x.LastSeenAt <= now ? "ONLINE" : "OFFLINE", x.IsActive, x.BatteryPercent, x.LastSeenAt });
        var rows = sensors.Concat(uavs).Concat(gateways);
        if (query.DeviceType is not null) rows = rows.Where(x => x.DeviceType == query.DeviceType);
        if (query.IsActive.HasValue) rows = rows.Where(x => x.IsActive == query.IsActive);
        if (!string.IsNullOrWhiteSpace(query.Search)) rows = rows.Where(x => x.Name.Contains(query.Search.Trim()) || x.Code.Contains(query.Search.Trim()));
        var count = await rows.CountAsync(ct);
        var items = await rows.OrderBy(x => x.DeviceType).ThenBy(x => x.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new DeviceReportRow(x.DeviceType, x.Id, x.Code, x.Name, x.Status, x.IsActive, x.BatteryPercent, x.LastSeenAt)).ToArrayAsync(ct);
        return new(items, count, query.Page, query.PageSize);
    }
}
