using FluentValidation;

namespace FarmMonitoring.Application.Features.Equipment;

public sealed class UavValidator : AbstractValidator<UavRequest>
{
    public UavValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(100).OverridePropertyName("code");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150).OverridePropertyName("name");
        RuleFor(x => x.Model).MaximumLength(150).OverridePropertyName("model");
        RuleFor(x => x.Status).NotEmpty().MaximumLength(50).OverridePropertyName("status");
        RuleFor(x => x.BatteryPercent).InclusiveBetween(0m, 100m).OverridePropertyName("batteryPercent");
    }
}
public sealed class GatewayValidator : AbstractValidator<GatewayRequest>
{
    public GatewayValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(100).OverridePropertyName("code");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150).OverridePropertyName("name");
        RuleFor(x => x.GatewayType).NotEmpty().MaximumLength(50).OverridePropertyName("gatewayType");
        RuleFor(x => x.Status).NotEmpty().MaximumLength(50).OverridePropertyName("status");
        RuleFor(x => x.UavId).GreaterThan(0).OverridePropertyName("uavId");
        RuleFor(x => x.BatteryPercent).InclusiveBetween(0m, 100m).OverridePropertyName("batteryPercent");
        RuleFor(x => x.FirmwareVersion).MaximumLength(100).OverridePropertyName("firmwareVersion");
    }
}
public sealed class EquipmentStatusValidator : AbstractValidator<EquipmentStatusRequest>
{
    public EquipmentStatusValidator()
    {
        RuleFor(x => x.Status).NotEmpty().MaximumLength(50).OverridePropertyName("status");
        RuleFor(x => x.IsActive).NotNull().OverridePropertyName("isActive");
    }
}
public sealed class AssignUavValidator : AbstractValidator<AssignUavRequest>
{
    public AssignUavValidator() => RuleFor(x => x.UavId).GreaterThan(0).OverridePropertyName("uavId");
}
