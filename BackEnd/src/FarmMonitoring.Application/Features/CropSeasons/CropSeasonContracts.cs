using FarmMonitoring.Application.Common;
using FluentValidation;

namespace FarmMonitoring.Application.Features.CropSeasons;

public sealed record CropSeasonRequest(int ExpectedRevision, int FarmId, int? ZoneId, string Name, string Crop,
    decimal? AreaHectares, DateOnly? StartDate, DateOnly? ExpectedEndDate, int? CropAgeDays, string? Notes);
public sealed record CompleteCropSeasonRequest(int ExpectedRevision, DateOnly? EndDate, decimal? YieldKg,
    decimal? TotalCostVnd, decimal? RevenueVnd, string? Notes);
public sealed record CultivationActivityRequest(int ExpectedRevision, DateOnly Date, string Type, string Description,
    decimal? CostVnd, string? Notes, EvidenceImageData[]? Images = null);

public sealed class CropSeasonValidator : AbstractValidator<CropSeasonRequest>
{
    public CropSeasonValidator()
    {
        RuleFor(x => x.ExpectedRevision).InclusiveBetween(0, int.MaxValue - 1);
        RuleFor(x => x.FarmId).GreaterThan(0);
        RuleFor(x => x.ZoneId).GreaterThan(0).When(x => x.ZoneId.HasValue);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Crop).NotEmpty().MaximumLength(200);
        RuleFor(x => x.AreaHectares).GreaterThan(0).PrecisionScale(18, 4, true);
        RuleFor(x => x.CropAgeDays).GreaterThanOrEqualTo(0);
        RuleFor(x => x).Must(x => !x.StartDate.HasValue || !x.ExpectedEndDate.HasValue || x.ExpectedEndDate >= x.StartDate)
            .WithMessage("Expected end date must not precede start date.");
        RuleFor(x => x.Notes).MaximumLength(4000);
    }
}
public sealed class CompleteCropSeasonValidator : AbstractValidator<CompleteCropSeasonRequest>
{
    public CompleteCropSeasonValidator()
    {
        RuleFor(x => x.ExpectedRevision).InclusiveBetween(1, int.MaxValue - 1);
        RuleFor(x => x.YieldKg).GreaterThanOrEqualTo(0).PrecisionScale(18, 4, true);
        RuleFor(x => x.TotalCostVnd).GreaterThanOrEqualTo(0).PrecisionScale(18, 2, true);
        RuleFor(x => x.RevenueVnd).GreaterThanOrEqualTo(0).PrecisionScale(18, 2, true);
        RuleFor(x => x.Notes).MaximumLength(4000);
    }
}
public sealed class CultivationActivityValidator : AbstractValidator<CultivationActivityRequest>
{
    public CultivationActivityValidator()
    {
        RuleFor(x => x.ExpectedRevision).InclusiveBetween(1, int.MaxValue - 1);
        RuleFor(x => x.Date).NotEmpty();
        RuleFor(x => x.Type).Must(x => x is "WATERING" or "FERTILIZING" or "PESTICIDE" or "INSPECTION" or "OTHER")
            .WithMessage("Choose watering, fertilizing, pesticide, inspection or other.");
        RuleFor(x => x.Description).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.CostVnd).GreaterThanOrEqualTo(0).PrecisionScale(18, 2, true);
        RuleFor(x => x.Notes).MaximumLength(4000);
    }
}
