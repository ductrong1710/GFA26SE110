namespace FarmMonitoring.Domain.Entities;

public sealed class Farm
{
    public ICollection<UserFarm> UserFarms { get; set; } = new List<UserFarm>();
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
