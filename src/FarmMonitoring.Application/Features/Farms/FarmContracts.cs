namespace FarmMonitoring.Application.Features.Farms;

public sealed record FarmRequest(string Name, string? Description, decimal? Latitude, decimal? Longitude);
public sealed record FarmStatusRequest(bool? IsActive);
public sealed record ZoneRequest(string Name, string? Description, decimal? CenterLatitude, decimal? CenterLongitude);
public sealed record FarmResponse(int Id, string Name, string? Description, decimal? Latitude, decimal? Longitude,
    bool IsActive, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);
public sealed record ZoneResponse(int Id, int FarmId, string Name, string? Description, decimal? CenterLatitude,
    decimal? CenterLongitude, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);
