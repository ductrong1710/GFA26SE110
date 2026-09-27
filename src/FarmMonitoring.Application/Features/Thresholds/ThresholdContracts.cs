using FluentValidation;

namespace FarmMonitoring.Application.Features.Thresholds;

public sealed record ThresholdRequest(decimal? MinValue, decimal? MaxValue, int? DataTimeoutMinutes,
    decimal? LowBatteryPercent, bool IsEnabled = true);
public sealed record ThresholdResponse(int Id, int SensorChannelId, decimal? MinValue, decimal? MaxValue,
    int? DataTimeoutMinutes, decimal? LowBatteryPercent, bool IsEnabled, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

public sealed class ThresholdValidator : AbstractValidator<ThresholdRequest>
{
    public ThresholdValidator()
    {
        RuleFor(x => x.MinValue).InclusiveBetween(-999999999999.999999m, 999999999999.999999m).OverridePropertyName("minValue");
        RuleFor(x => x.MaxValue).InclusiveBetween(-999999999999.999999m, 999999999999.999999m).OverridePropertyName("maxValue");
        RuleFor(x => x.MinValue).LessThanOrEqualTo(x => x.MaxValue)
            .When(x => x.MinValue.HasValue && x.MaxValue.HasValue).OverridePropertyName("minValue");
        RuleFor(x => x.DataTimeoutMinutes).GreaterThan(0).OverridePropertyName("dataTimeoutMinutes");
        RuleFor(x => x.LowBatteryPercent).InclusiveBetween(0m, 100m).OverridePropertyName("lowBatteryPercent");
    }
}
