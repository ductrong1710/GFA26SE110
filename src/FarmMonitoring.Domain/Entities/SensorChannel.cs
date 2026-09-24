namespace FarmMonitoring.Domain.Entities;

public sealed class SensorChannel
{
    public int Id { get; set; }
    public int SensorNodeId { get; set; }
    public SensorNode SensorNode { get; set; } = null!;
    public int SensorTypeId { get; set; }
    public SensorType SensorType { get; set; } = null!;
    public string ChannelCode { get; set; } = "";
    public string? Name { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
