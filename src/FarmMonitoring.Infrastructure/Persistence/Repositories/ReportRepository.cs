using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Reports;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FarmMonitoring.Infrastructure.Persistence.Repositories;

public sealed class ReportRepository(AppDbContext db) : IReportRepository
{
    public async Task<DashboardOverview> DashboardAsync(CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
        var farms = await db.Farms.CountAsync(ct);
        var sensors = await db.SensorNodes.GroupBy(x => 1).Select(g => new DeviceCounts(g.Count(),
            g.Count(x => x.IsActive && x.Status.ToUpper() == "ONLINE"), g.Count(x => x.IsActive && x.Status.ToUpper() == "OFFLINE"),
            g.Count(x => x.IsActive && x.Status.ToUpper() != "ONLINE" && x.Status.ToUpper() != "OFFLINE"), g.Count(x => !x.IsActive)))
            .SingleOrDefaultAsync(ct) ?? new(0, 0, 0, 0, 0);
        var gateways = await db.Gateways.GroupBy(x => 1).Select(g => new DeviceCounts(g.Count(),
            g.Count(x => x.IsActive && x.Status.ToUpper() == "ONLINE"), g.Count(x => x.IsActive && x.Status.ToUpper() == "OFFLINE"),
            g.Count(x => x.IsActive && x.Status.ToUpper() != "ONLINE" && x.Status.ToUpper() != "OFFLINE"), g.Count(x => !x.IsActive)))
            .SingleOrDefaultAsync(ct) ?? new(0, 0, 0, 0, 0);
        var missions = await db.Missions.GroupBy(x => 1).Select(g => new MissionCounts(g.Count(x => x.Status == MissionStatus.SCHEDULED),
            g.Count(x => x.Status == MissionStatus.RUNNING), g.Count(x => x.Status == MissionStatus.FAILED))).SingleOrDefaultAsync(ct) ?? new(0, 0, 0);
        var alerts = await db.Alerts.GroupBy(x => 1).Select(g => new AlertCounts(g.Count(x => x.Status == AlertStatus.OPEN),
            g.Count(x => x.Status != AlertStatus.CLOSED && x.Severity == AlertSeverity.CRITICAL), g.Count(x => x.Status == AlertStatus.ACKNOWLEDGED))).SingleOrDefaultAsync(ct) ?? new(0, 0, 0);
        await transaction.CommitAsync(ct);
        return new(farms, sensors, gateways, missions, alerts);
    }
    public async Task<PagedResult<SensorReportRow>> SensorsAsync(ReportQuery query, ReportPeriod period, CancellationToken ct)
    {
        var rows = db.SensorReadings.AsNoTracking().Where(x => x.IsValid && x.MeasuredAt >= period.From && x.MeasuredAt <= period.To);
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
    public async Task<PagedResult<MissionReportRow>> MissionsAsync(ReportQuery query, ReportPeriod period, CancellationToken ct)
    {
        var rows = db.Missions.AsNoTracking().Where(x => x.CreatedAt >= period.From && x.CreatedAt <= period.To);
        if (query.FarmId.HasValue) rows = rows.Where(x => x.FarmId == query.FarmId);
        if (!string.IsNullOrWhiteSpace(query.Search)) rows = rows.Where(x => x.Name.Contains(query.Search.Trim()));
        var count = await rows.CountAsync(ct);
        var items = await rows.OrderByDescending(x => x.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new MissionReportRow(x.Id, x.Name, x.FarmId, x.Status.ToString(), x.CreatedAt, x.StartedAt, x.CompletedAt, x.Targets.Count,
                x.Targets.Count(t => t.Status == MissionTargetStatus.COLLECTED), x.Targets.Count(t => t.Status == MissionTargetStatus.FAILED), x.Targets.Count(t => t.Status == MissionTargetStatus.SKIPPED))).ToArrayAsync(ct);
        return new(items, count, query.Page, query.PageSize);
    }
    public async Task<PagedResult<AlertReportRow>> AlertsAsync(ReportQuery query, ReportPeriod period, bool operationalOnly, CancellationToken ct)
    {
        var rows = db.Alerts.AsNoTracking().Where(x => x.OpenedAt >= period.From && x.OpenedAt <= period.To);
        if (operationalOnly) rows = rows.Where(x => x.AlertType == AlertType.MISSION_ERROR || x.AlertType == AlertType.GATEWAY_ERROR || x.AlertType == AlertType.GATEWAY_OFFLINE || x.AlertType == AlertType.UAV_LOW_BATTERY);
        if (query.FarmId.HasValue) rows = rows.Where(x => (x.SensorNode != null && x.SensorNode.Zone.FarmId == query.FarmId) || (x.Mission != null && x.Mission.FarmId == query.FarmId));
        if (query.ZoneId.HasValue) rows = rows.Where(x => x.SensorNode != null && x.SensorNode.ZoneId == query.ZoneId);
        if (!string.IsNullOrWhiteSpace(query.Search)) rows = rows.Where(x => x.Message.Contains(query.Search.Trim()));
        var groups = rows.GroupBy(x => new { x.AlertType, x.Severity, x.Status });
        var count = await groups.CountAsync(ct);
        var items = await groups.OrderBy(g => g.Key.AlertType).ThenBy(g => g.Key.Severity).ThenBy(g => g.Key.Status)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).Select(g => new AlertReportRow(g.Key.AlertType.ToString(),
                g.Key.Severity.ToString(), g.Key.Status.ToString(), g.LongCount(), g.Min(x => x.OpenedAt), g.Max(x => x.OpenedAt))).ToArrayAsync(ct);
        return new(items, count, query.Page, query.PageSize);
    }
    public async Task<PagedResult<DeviceReportRow>> DevicesAsync(DeviceReportQuery query, CancellationToken ct)
    {
        var sensors = db.SensorNodes.AsNoTracking().Select(x => new { DeviceType = "SENSOR_NODE", x.Id, Code = x.DeviceCode, x.Name, x.Status, x.IsActive, x.BatteryPercent, x.LastSeenAt });
        var uavs = db.Uavs.AsNoTracking().Select(x => new { DeviceType = "UAV", x.Id, x.Code, x.Name, x.Status, x.IsActive, x.BatteryPercent, x.LastSeenAt });
        var gateways = db.Gateways.AsNoTracking().Select(x => new { DeviceType = "GATEWAY", x.Id, x.Code, x.Name, x.Status, x.IsActive, x.BatteryPercent, x.LastSeenAt });
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
