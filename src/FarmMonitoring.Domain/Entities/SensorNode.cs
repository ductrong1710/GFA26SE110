namespace FarmMonitoring.Domain.Entities;

public sealed class SensorNode
{
    public int Id { get; set; }
    public int ZoneId { get; set; }
    public Zone Zone { get; set; } = null!;
    public string DeviceCode { get; set; } = "";
    public string Name { get; set; } = "";
    public string Status { get; set; } = "";
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public decimal? LocalX { get; set; }
    public decimal? LocalY { get; set; }
    public DateTimeOffset? LastSeenAt { get; set; }
    public decimal? BatteryPercent { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
