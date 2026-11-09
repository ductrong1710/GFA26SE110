namespace FarmMonitoring.Domain.Entities;

public sealed class ThresholdChange
{
    public int Id { get; set; }
    public int SensorChannelId { get; set; }
    public int? ChangedByUserId { get; set; }
    public DateTimeOffset ChangedAt { get; set; }
    public decimal? MinValue { get; set; }
    public decimal? MaxValue { get; set; }
    public int? DataTimeoutMinutes { get; set; }
    public decimal? LowBatteryPercent { get; set; }
    public bool IsEnabled { get; set; }
}
