using FluentValidation;

namespace FarmMonitoring.Application.Features.Farms;

public sealed class FarmValidator : AbstractValidator<FarmRequest>
{
    public FarmValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150).OverridePropertyName("name");
        RuleFor(x => x.Latitude).InclusiveBetween(-90m, 90m).OverridePropertyName("latitude");
        RuleFor(x => x.Longitude).InclusiveBetween(-180m, 180m).OverridePropertyName("longitude");
    }
}

public sealed class ZoneValidator : AbstractValidator<ZoneRequest>
{
    public ZoneValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150).OverridePropertyName("name");
        RuleFor(x => x.CenterLatitude).InclusiveBetween(-90m, 90m).OverridePropertyName("centerLatitude");
        RuleFor(x => x.CenterLongitude).InclusiveBetween(-180m, 180m).OverridePropertyName("centerLongitude");
    }
}

public sealed class FarmStatusValidator : AbstractValidator<FarmStatusRequest>
{
    public FarmStatusValidator() => RuleFor(x => x.IsActive).NotNull().OverridePropertyName("isActive");
}
