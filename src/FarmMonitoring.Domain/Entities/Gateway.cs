namespace FarmMonitoring.Domain.Entities;

public sealed class Gateway
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string GatewayType { get; set; } = "";
    public string Status { get; set; } = "";
    public int? UavId { get; set; }
    public Uav? Uav { get; set; }
    public DateTimeOffset? LastSeenAt { get; set; }
    public decimal? BatteryPercent { get; set; }
    public string? FirmwareVersion { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
