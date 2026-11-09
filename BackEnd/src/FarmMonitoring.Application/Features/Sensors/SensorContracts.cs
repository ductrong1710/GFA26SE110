using FarmMonitoring.Application.Common;

namespace FarmMonitoring.Application.Features.Sensors;

public sealed class SensorNodeQuery : PageQuery
{
    public int? FarmId { get; init; }
    public int? ZoneId { get; init; }
    public bool? IsActive { get; init; }
}
public sealed record SensorTypeRequest(string Code, string Name, string? Unit, string? Description);
public sealed record SensorTypeResponse(int Id, string Code, string Name, string? Unit, string? Description);
public sealed record SensorNodeRequest(int ZoneId, string DeviceCode, string Name, string Status,
    decimal? Latitude, decimal? Longitude, decimal? LocalX, decimal? LocalY, DateTimeOffset? LastSeenAt, decimal? BatteryPercent);
public sealed record SensorNodeStatusRequest(string Status, bool? IsActive);
public sealed record SensorNodeResponse(int Id, int ZoneId, string DeviceCode, string Name, string Status,
    decimal? Latitude, decimal? Longitude, decimal? LocalX, decimal? LocalY, DateTimeOffset? LastSeenAt,
    decimal? BatteryPercent, bool IsActive, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);
public sealed record SensorChannelRequest(int SensorTypeId, string ChannelCode, string? Name, bool IsActive = true);
public sealed record SensorChannelResponse(int Id, int SensorNodeId, int SensorTypeId, string ChannelCode,
    string? Name, bool IsActive, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);
