using FarmMonitoring.Application.Common;

namespace FarmMonitoring.Application.Features.Reports;

public sealed class ReportQuery : PageQuery
{
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
    public int? FarmId { get; init; }
    public int? ZoneId { get; init; }
    public int? SensorTypeId { get; init; }
}
public sealed record ReportPeriod(DateTimeOffset From, DateTimeOffset To);
public sealed class DeviceReportQuery : PageQuery
{
    public string? DeviceType { get; init; }
    public bool? IsActive { get; init; }
}
public sealed record DeviceCounts(int Total, int Online, int Offline, int Other, int Inactive);
public sealed record MissionCounts(int Scheduled, int Running, int Failed);
public sealed record AlertCounts(int Open, int Critical, int Acknowledged);
public sealed record DashboardOverview(int Farms, DeviceCounts SensorNodes, DeviceCounts Gateways, MissionCounts Missions, AlertCounts Alerts);
public sealed record SensorReportRow(int SensorChannelId, int SensorNodeId, string ChannelCode, string? Unit, int ZoneId,
    long ReadingCount, decimal Minimum, decimal Maximum, decimal Average, DateTimeOffset FirstMeasuredAt, DateTimeOffset LastMeasuredAt);
public sealed record MissionReportRow(int Id, string Name, int FarmId, string Status, DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt, DateTimeOffset? CompletedAt, int TotalTargets, int SuccessfulTargets, int FailedTargets, int SkippedTargets);
public sealed record AlertReportRow(string AlertType, string Severity, string Status, long Count, DateTimeOffset FirstOpenedAt, DateTimeOffset LastOpenedAt);
public sealed record DeviceReportRow(string DeviceType, int Id, string Code, string Name, string Status, bool IsActive, decimal? BatteryPercent, DateTimeOffset? LastSeenAt);
