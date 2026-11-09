namespace FarmMonitoring.Domain.Entities;

public sealed class SensorThreshold
{
    public int Id { get; set; }
    public int SensorChannelId { get; set; }
    public SensorChannel SensorChannel { get; set; } = null!;
    public decimal? MinValue { get; set; }
    public decimal? MaxValue { get; set; }
    public int? DataTimeoutMinutes { get; set; }
    public decimal? LowBatteryPercent { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
