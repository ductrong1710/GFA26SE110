namespace FarmMonitoring.Domain.Entities;

public sealed class TelemetryRecord
{
    public long Id { get; set; }
    public int MissionId { get; set; }
    public Mission Mission { get; set; } = null!;
    public int? UavId { get; set; }
    public Uav? Uav { get; set; }
    public int? GatewayId { get; set; }
    public Gateway? Gateway { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public decimal? LocalX { get; set; }
    public decimal? LocalY { get; set; }
    public decimal? AltitudeM { get; set; }
    public decimal? BatteryPercent { get; set; }
    public int? CurrentWaypointNo { get; set; }
    public string? FlightStatus { get; set; }
}
