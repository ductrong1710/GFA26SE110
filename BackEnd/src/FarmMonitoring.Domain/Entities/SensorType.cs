namespace FarmMonitoring.Domain.Entities;

public sealed class SensorType
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Unit { get; set; }
    public string? Description { get; set; }
}
