namespace FarmMonitoring.Domain.Entities;

public sealed class SensorReading
{
    public long Id { get; set; }
    public int SensorChannelId { get; set; }
    public SensorChannel SensorChannel { get; set; } = null!;
    public int? GatewayId { get; set; }
    public Gateway? Gateway { get; set; }
    public int? MissionId { get; set; }
    public Mission? Mission { get; set; }
    public string SourceRecordKey { get; set; } = "";
    public decimal Value { get; set; }
    public DateTimeOffset MeasuredAt { get; set; }
    public DateTimeOffset CollectedAt { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
    public string? QualityStatus { get; set; }
    public bool IsValid { get; set; } = true;
    public string? ValidationError { get; set; }
}
