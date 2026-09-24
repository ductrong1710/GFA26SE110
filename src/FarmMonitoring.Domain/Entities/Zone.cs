namespace FarmMonitoring.Domain.Entities;

public sealed class Zone
{
    public int Id { get; set; }
    public int FarmId { get; set; }
    public Farm Farm { get; set; } = null!;
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public decimal? CenterLatitude { get; set; }
    public decimal? CenterLongitude { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
