using FluentValidation;

namespace FarmMonitoring.Application.Features.Sensors;

public sealed class SensorTypeValidator : AbstractValidator<SensorTypeRequest>
{
    public SensorTypeValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(100).OverridePropertyName("code");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150).OverridePropertyName("name");
        RuleFor(x => x.Unit).MaximumLength(50).OverridePropertyName("unit");
        RuleFor(x => x.Description).MaximumLength(255).OverridePropertyName("description");
    }
}

public sealed class SensorNodeValidator : AbstractValidator<SensorNodeRequest>
{
    public SensorNodeValidator()
    {
        RuleFor(x => x.ZoneId).GreaterThan(0).OverridePropertyName("zoneId");
        RuleFor(x => x.DeviceCode).NotEmpty().MaximumLength(100).OverridePropertyName("deviceCode");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150).OverridePropertyName("name");
        RuleFor(x => x.Status).NotEmpty().MaximumLength(50).OverridePropertyName("status");
        RuleFor(x => x.Latitude).InclusiveBetween(-90m, 90m).OverridePropertyName("latitude");
        RuleFor(x => x.Longitude).InclusiveBetween(-180m, 180m).OverridePropertyName("longitude");
        RuleFor(x => x.LocalX).InclusiveBetween(-9999999.999m, 9999999.999m).OverridePropertyName("localX");
        RuleFor(x => x.LocalY).InclusiveBetween(-9999999.999m, 9999999.999m).OverridePropertyName("localY");
        RuleFor(x => x.BatteryPercent).InclusiveBetween(0m, 100m).OverridePropertyName("batteryPercent");
    }
}

public sealed class SensorNodeStatusValidator : AbstractValidator<SensorNodeStatusRequest>
{
    public SensorNodeStatusValidator()
    {
        RuleFor(x => x.Status).NotEmpty().MaximumLength(50).OverridePropertyName("status");
        RuleFor(x => x.IsActive).NotNull().OverridePropertyName("isActive");
    }
}

public sealed class SensorChannelValidator : AbstractValidator<SensorChannelRequest>
{
    public SensorChannelValidator()
    {
        RuleFor(x => x.SensorTypeId).GreaterThan(0).OverridePropertyName("sensorTypeId");
        RuleFor(x => x.ChannelCode).NotEmpty().MaximumLength(100).OverridePropertyName("channelCode");
        RuleFor(x => x.Name).MaximumLength(150).OverridePropertyName("name");
    }
}
