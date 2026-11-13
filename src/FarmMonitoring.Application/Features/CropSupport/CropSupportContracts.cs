using FarmMonitoring.Application.Common;
using FluentValidation;
using System.Globalization;

namespace FarmMonitoring.Application.Features.CropSupport;

public sealed record CreateCropConditionRequest(int FarmId, int? ZoneId, string Crop, string Description,
    int[]? SensorChannelIds = null, EvidenceImageData[]? Images = null, int? CropSeasonId = null, string? ObservedAt = null);
public sealed record CropConsultationRequest(int ExpectedRevision, string Content, bool RecommendSiteSurvey = false,
    EvidenceImageData[]? Images = null, string? FollowUpType = null, int? CultivationActivityId = null,
    int? EngineerId = null, string? ReferralType = null, int? DeviceIncidentId = null);
public sealed record AssignCropReportRequest(int ExpectedRevision, int EngineerId);
public sealed record CropFollowUpRequest(int ExpectedRevision, string Type, string Content,
    EvidenceImageData[]? Images = null, int? CultivationActivityId = null);
public sealed record CropReferralRequest(int ExpectedRevision, string Type, string Content, int? DeviceIncidentId = null,
    EvidenceImageData[]? Images = null);

public sealed class CropEvidenceQuery : PageQuery
{
    public string? From { get; init; }
    public string? To { get; init; }
    public int? SensorChannelId { get; init; }
}

public static class CropObservationTime
{
    // Like sync timestamps, transport values must include an explicit timezone.
    public static DateTimeOffset? Parse(string? text)
    {
        if (text is null) return null;
        if (!text.Contains('T') || !(text.EndsWith('Z') || (text.Length >= 6 && text[^3] == ':' && text[^6] is '+' or '-'))
            || !DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var value))
            throw new ValidationException("Time must be ISO 8601 with Z or an explicit offset.");
        return new DateTimeOffset(value.UtcTicks - value.UtcTicks % 10, TimeSpan.Zero);
    }
    public static void Validate(CropEvidenceQuery query)
    {
        var from = Parse(query.From); var to = Parse(query.To);
        if (from.HasValue != to.HasValue || from > to || to - from > TimeSpan.FromDays(31))
            throw new ValidationException("Supply both from and to, ordered and at most 31 days apart.");
        if (query.SensorChannelId <= 0) throw new ValidationException("Sensor channel ID must be positive.");
    }
    public static (DateTimeOffset? From, DateTimeOffset? To) Window(CropEvidenceQuery query, DateTimeOffset? observedAt)
        => (Parse(query.From) ?? observedAt?.AddDays(-7), Parse(query.To) ?? observedAt?.AddDays(1));
}

public sealed class CreateCropConditionValidator : AbstractValidator<CreateCropConditionRequest>
{
    public CreateCropConditionValidator()
    {
        RuleFor(x => x.FarmId).GreaterThan(0);
        RuleFor(x => x.ZoneId).GreaterThan(0).When(x => x.ZoneId.HasValue);
        RuleFor(x => x.CropSeasonId).GreaterThan(0).When(x => x.CropSeasonId.HasValue);
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
