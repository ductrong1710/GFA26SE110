using FarmMonitoring.Application.Features.CropSeasons;

namespace FarmMonitoring.UnitTests;
public sealed class CropSeasonValidationTests
{
    [Fact]
    public void Season_preserves_unknown_dates_and_age_but_rejects_invalid_known_values()
    {
        var validator = new CropSeasonValidator();
        var request = new CropSeasonRequest(0, 1, null, "Winter crop", "Rice", null, null, null, null, null);
        Assert.True(validator.Validate(request).IsValid);
        Assert.True(validator.Validate(request with { CropAgeDays = 0 }).IsValid);
        Assert.False(validator.Validate(request with { FarmId = 0 }).IsValid);
        Assert.False(validator.Validate(request with { ZoneId = 0 }).IsValid);
        Assert.False(validator.Validate(request with { Name = " " }).IsValid);
        Assert.False(validator.Validate(request with { Crop = " " }).IsValid);
        Assert.False(validator.Validate(request with { AreaHectares = 0 }).IsValid);
        Assert.False(validator.Validate(request with { CropAgeDays = -1 }).IsValid);
        Assert.False(validator.Validate(request with { StartDate = new(2026, 11, 2), ExpectedEndDate = new(2026, 11, 1) }).IsValid);
        Assert.False(validator.Validate(request with { ExpectedRevision = int.MaxValue }).IsValid);
    }
    [Fact]
    public void Completion_accepts_unknown_and_zero_results_without_estimating_them()
    {
        var validator = new CompleteCropSeasonValidator();
        var request = new CompleteCropSeasonRequest(1, null, null, null, null, null);
        Assert.True(validator.Validate(request).IsValid);
        Assert.True(validator.Validate(request with { YieldKg = 0, TotalCostVnd = 0, RevenueVnd = 0 }).IsValid);
        Assert.False(validator.Validate(request with { YieldKg = -1 }).IsValid);
        Assert.False(validator.Validate(request with { TotalCostVnd = -1 }).IsValid);
        Assert.False(validator.Validate(request with { RevenueVnd = -1 }).IsValid);
        Assert.False(validator.Validate(request with { ExpectedRevision = 0 }).IsValid);
        Assert.False(validator.Validate(request with { TotalCostVnd = 1.001m }).IsValid);
    }
    [Fact]
    public void Activity_requires_known_taxonomy_date_and_description_and_keeps_cost_optional()
    {
        var validator = new CultivationActivityValidator();
        var request = new CultivationActivityRequest(1, new(2026, 11, 2), "WATERING", "Watered north beds", null, null);
        foreach (var type in new[] { "WATERING", "FERTILIZING", "PESTICIDE", "INSPECTION", "OTHER" })
            Assert.True(validator.Validate(request with { Type = type }).IsValid);
        Assert.True(validator.Validate(request with { CostVnd = 0 }).IsValid);
        Assert.False(validator.Validate(request with { Type = "HARVEST_ESTIMATE" }).IsValid);
        Assert.False(validator.Validate(request with { Date = default }).IsValid);
        Assert.False(validator.Validate(request with { CostVnd = -1 }).IsValid);
        Assert.False(validator.Validate(request with { Description = " " }).IsValid);
        Assert.False(validator.Validate(request with { ExpectedRevision = 0 }).IsValid);
    }
}
