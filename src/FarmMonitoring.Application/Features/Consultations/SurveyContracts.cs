using FluentValidation;
using FarmMonitoring.Application.Common;

namespace FarmMonitoring.Application.Features.Consultations;

public sealed record SurveyZoneRequest(string Name, string? Crop, int? CropAgeDays, string? Symptoms, string InstallationLocation);
public sealed record SurveyRequest(int ExpectedRevision, string FarmName, string FarmLocation, decimal? FarmArea,
    string MonitoringNeeds, string? Notes, SurveyZoneRequest[] Zones, EvidenceImageData[]? Images = null);
public sealed record SurveySummary(int Id, int Revision, string FarmName, int EngineerId, DateTimeOffset CreatedAt);
public sealed record SurveyZoneResponse(int Id, string Name, string? Crop, int? CropAgeDays, string? Symptoms, string InstallationLocation);
public sealed record SurveyResponse(int Id, int ConsultationId, int Revision, int EngineerId, string FarmName,
    string FarmLocation, decimal? FarmArea, string MonitoringNeeds, string? Notes, DateTimeOffset CreatedAt, IReadOnlyList<SurveyZoneResponse> Zones, IReadOnlyList<EvidenceImageData> Images);

public sealed class SurveyValidator : AbstractValidator<SurveyRequest>
{
    public SurveyValidator()
    {
        RuleFor(x => x.ExpectedRevision).InclusiveBetween(0, int.MaxValue - 1);
        RuleFor(x => x.FarmName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.FarmLocation).NotEmpty().MaximumLength(500);
        RuleFor(x => x.FarmArea).GreaterThan(0).LessThanOrEqualTo(100000000);
        RuleFor(x => x.MonitoringNeeds).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.Notes).MaximumLength(4000);
        RuleFor(x => x.Zones).NotNull().Must(x => x is { Length: > 0 and <= 100 }).WithMessage("Provide 1 to 100 survey zones.");
        RuleForEach(x => x.Zones).NotNull().SetValidator(new SurveyZoneValidator());
        RuleFor(x => x.Zones).Must(zones => zones is null || zones.Where(x => x is not null)
            .Select(x => x.Name?.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() == zones.Length)
            .WithMessage("Zone names must be unique within a survey.");
    }
}

public sealed class SurveyZoneValidator : AbstractValidator<SurveyZoneRequest>
{
    public SurveyZoneValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Crop).MaximumLength(150);
        RuleFor(x => x.CropAgeDays).InclusiveBetween(0, 36500);
        RuleFor(x => x.Symptoms).MaximumLength(2000);
        RuleFor(x => x.InstallationLocation).NotEmpty().MaximumLength(1000);
    }
}
