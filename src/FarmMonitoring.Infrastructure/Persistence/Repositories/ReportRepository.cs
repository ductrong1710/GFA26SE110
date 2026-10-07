using FarmMonitoring.Application.Features.Farms;
using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Reports;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FarmMonitoring.Infrastructure.Persistence.Repositories;

public sealed class ReportRepository(AppDbContext db) : IReportRepository
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
        var missionRows = db.Missions.AsNoTracking().Where(x => farmIds.Contains(x.FarmId));
        var alertRows = db.Alerts.AsNoTracking().ForFarms(db, scope, FarmAccessQuery.AlertFarm);
        if (farmId.HasValue) alertRows = AlertsInFarm(alertRows, farmId.Value);
        var gatewayRows = db.Gateways.AsNoTracking();
        if (!scope.IsAdministrator || farmId.HasValue) gatewayRows = gatewayRows.Where(x => missionRows.Any(m => m.GatewayId == x.Id));
        var farms = await farmRows.CountAsync(ct);
        var sensors = await db.SensorNodes.Where(x => farmIds.Contains(x.Zone.FarmId)).GroupBy(x => 1).Select(g => new DeviceCounts(g.Count(),
            g.Count(x => x.IsActive && x.Status.ToUpper() == "ONLINE"), g.Count(x => x.IsActive && x.Status.ToUpper() == "OFFLINE"),
            g.Count(x => x.IsActive && x.Status.ToUpper() != "ONLINE" && x.Status.ToUpper() != "OFFLINE"), g.Count(x => !x.IsActive)))
            .SingleOrDefaultAsync(ct) ?? new(0, 0, 0, 0, 0);
        var gateways = await gatewayRows.GroupBy(x => 1).Select(g => new DeviceCounts(g.Count(),
            g.Count(x => x.IsActive && x.Status.ToUpper() == "ONLINE"), g.Count(x => x.IsActive && x.Status.ToUpper() == "OFFLINE"),
            g.Count(x => x.IsActive && x.Status.ToUpper() != "ONLINE" && x.Status.ToUpper() != "OFFLINE"), g.Count(x => !x.IsActive)))
            .SingleOrDefaultAsync(ct) ?? new(0, 0, 0, 0, 0);
        var missions = await missionRows.GroupBy(x => 1).Select(g => new MissionCounts(g.Count(x => x.Status == MissionStatus.SCHEDULED),
            g.Count(x => x.Status == MissionStatus.RUNNING), g.Count(x => x.Status == MissionStatus.FAILED))).SingleOrDefaultAsync(ct) ?? new(0, 0, 0);
        var alerts = await alertRows.GroupBy(x => 1).Select(g => new AlertCounts(g.Count(x => x.Status == AlertStatus.OPEN),
            g.Count(x => x.Status != AlertStatus.CLOSED && x.Severity == AlertSeverity.CRITICAL), g.Count(x => x.Status == AlertStatus.ACKNOWLEDGED))).SingleOrDefaultAsync(ct) ?? new(0, 0, 0);
        await transaction.CommitAsync(ct);
        return new(farms, sensors, gateways, missions, alerts);
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
        var rows = db.Missions.AsNoTracking().ForFarms(db, scope, x => x.FarmId).Where(x => x.CreatedAt >= period.From && x.CreatedAt <= period.To);
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
        var missionRows = db.Missions.AsNoTracking().ForFarms(db, scope, x => x.FarmId);
        if (query.FarmId.HasValue)
        {
            sensorRows = sensorRows.Where(x => x.Zone.FarmId == query.FarmId);
            missionRows = missionRows.Where(x => x.FarmId == query.FarmId);
        }
        var uavRows = db.Uavs.AsNoTracking();
        var gatewayRows = db.Gateways.AsNoTracking();
        if (!scope.IsAdministrator || query.FarmId.HasValue)
        {
            uavRows = uavRows.Where(x => missionRows.Any(m => m.UavId == x.Id));
            gatewayRows = gatewayRows.Where(x => missionRows.Any(m => m.GatewayId == x.Id));
        }
        var sensors = sensorRows.Select(x => new { DeviceType = "SENSOR_NODE", x.Id, Code = x.DeviceCode, x.Name, x.Status, x.IsActive, x.BatteryPercent, x.LastSeenAt });
        var uavs = uavRows.Select(x => new { DeviceType = "UAV", x.Id, x.Code, x.Name, x.Status, x.IsActive, x.BatteryPercent, x.LastSeenAt });
        var gateways = gatewayRows.Select(x => new { DeviceType = "GATEWAY", x.Id, x.Code, x.Name, x.Status, x.IsActive, x.BatteryPercent, x.LastSeenAt });
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
