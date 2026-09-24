namespace FarmMonitoring.Application.Features.Equipment;

public sealed record UavRequest(string Code, string Name, string? Model, string Status, decimal? BatteryPercent, DateTimeOffset? LastSeenAt);
public sealed record GatewayRequest(string Code, string Name, string GatewayType, string Status, int? UavId,
    decimal? BatteryPercent, DateTimeOffset? LastSeenAt, string? FirmwareVersion);
public sealed record EquipmentStatusRequest(string Status, bool? IsActive);
public sealed record AssignUavRequest(int? UavId);
public sealed record UavResponse(int Id, string Code, string Name, string? Model, string Status, decimal? BatteryPercent,
    DateTimeOffset? LastSeenAt, bool IsActive, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);
public sealed record GatewayResponse(int Id, string Code, string Name, string GatewayType, string Status, int? UavId,
    decimal? BatteryPercent, DateTimeOffset? LastSeenAt, string? FirmwareVersion, bool IsActive, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);
