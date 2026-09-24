namespace FarmMonitoring.Domain.Entities;

public sealed class Uav
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Model { get; set; }
    public string Status { get; set; } = "";
    public decimal? BatteryPercent { get; set; }
    public DateTimeOffset? LastSeenAt { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
