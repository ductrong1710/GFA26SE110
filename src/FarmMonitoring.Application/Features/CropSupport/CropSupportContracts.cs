using FarmMonitoring.Application.Common;
using FluentValidation;

namespace FarmMonitoring.Application.Features.CropSupport;

public sealed record CreateCropConditionRequest(int FarmId, int? ZoneId, string Crop, string Description,
    int[]? SensorChannelIds = null, EvidenceImageData[]? Images = null);
public sealed record CropConsultationRequest(int ExpectedRevision, string Content, bool RecommendSiteSurvey = false,
    EvidenceImageData[]? Images = null);

public sealed class CreateCropConditionValidator : AbstractValidator<CreateCropConditionRequest>
{
    public CreateCropConditionValidator()
    {
        RuleFor(x => x.FarmId).GreaterThan(0);
        RuleFor(x => x.ZoneId).GreaterThan(0).When(x => x.ZoneId.HasValue);
        RuleFor(x => x.Crop).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.SensorChannelIds).Must(ids => ids is null || ids.Length <= 50 && ids.All(id => id > 0) && ids.Distinct().Count() == ids.Length)
            .WithMessage("Choose at most 50 distinct sensor channels with valid IDs.");
    }
}
public sealed class CropConsultationValidator : AbstractValidator<CropConsultationRequest>
{
    public CropConsultationValidator()
    {
        RuleFor(x => x.ExpectedRevision).InclusiveBetween(1, int.MaxValue - 1);
        RuleFor(x => x.Content).NotEmpty().MaximumLength(4000);
    }
}
