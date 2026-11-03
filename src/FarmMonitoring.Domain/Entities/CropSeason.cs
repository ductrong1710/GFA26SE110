namespace FarmMonitoring.Domain.Entities;

public enum CropSeasonStatus { ACTIVE, COMPLETED }
public enum CultivationActivityType { WATERING, FERTILIZING, PESTICIDE, INSPECTION, OTHER }
public sealed class CropSeason
{
    public int Id { get; set; }
    public int FarmId { get; set; }
    public int? ZoneId { get; set; }
    public int OwnerId { get; set; }
    public string Name { get; set; } = "";
    public string Crop { get; set; } = "";
    public decimal? AreaHectares { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? ExpectedEndDate { get; set; }
    public int? CropAgeDays { get; set; }
    public string? Notes { get; set; }
    public CropSeasonStatus Status { get; set; } = CropSeasonStatus.ACTIVE;
    public int Revision { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateOnly? EndDate { get; set; }
    public decimal? YieldKg { get; set; }
    public decimal? TotalCostVnd { get; set; }
    public decimal? RevenueVnd { get; set; }
    public string? CompletionNotes { get; set; }
    public List<CultivationActivity> Activities { get; set; } = [];
}
public sealed class CultivationActivity
{
    public int Id { get; set; }
    public int SeasonId { get; set; }
    public CropSeason Season { get; set; } = null!;
    public int UserId { get; set; }
    public DateOnly Date { get; set; }
    public CultivationActivityType Type { get; set; }
    public string Description { get; set; } = "";
    public decimal? CostVnd { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public List<EvidenceImage> Images { get; set; } = [];
}
