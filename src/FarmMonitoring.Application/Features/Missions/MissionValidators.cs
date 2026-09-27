using FarmMonitoring.Application.Common;
using FarmMonitoring.Domain.Entities;
using FluentValidation;

namespace FarmMonitoring.Application.Features.Missions;

public sealed class WaypointValidator : AbstractValidator<WaypointRequest>
{
    public WaypointValidator()
    {
        RuleFor(x => x.SequenceNo).GreaterThan(0);
        RuleFor(x => x.Latitude).InclusiveBetween(-90m, 90m);
        RuleFor(x => x.Longitude).InclusiveBetween(-180m, 180m);
        RuleFor(x => x.LocalX).InclusiveBetween(-9999999.999m, 9999999.999m);
        RuleFor(x => x.LocalY).InclusiveBetween(-9999999.999m, 9999999.999m);
        RuleFor(x => x.AltitudeM).InclusiveBetween(0m, 9999999.999m);
        RuleFor(x => x.ActionType).MaximumLength(50);
        RuleFor(x => x.PlannedHoldSeconds).GreaterThanOrEqualTo(0);
        RuleFor(x => x).Must(x => x.Latitude.HasValue == x.Longitude.HasValue && x.LocalX.HasValue == x.LocalY.HasValue
            && (x.Latitude.HasValue || x.LocalX.HasValue)).WithMessage("A complete GPS or local coordinate pair is required.");
    }
}

public sealed class MissionValidator : AbstractValidator<MissionRequest>
{
    public MissionValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.FarmId).GreaterThan(0);
        RuleFor(x => x.UavId).GreaterThan(0);
        RuleFor(x => x.GatewayId).GreaterThan(0);
        RuleFor(x => x.OperatorNotes).MaximumLength(10000);
        RuleFor(x => x.SensorNodeIds).NotNull().Must(x => x is null || (x.Length <= 1000 && x.All(id => id > 0) && x.Distinct().Count() == x.Length))
            .WithMessage("Provide at most 1000 unique positive sensor IDs.");
        RuleFor(x => x.Waypoints).NotNull().Must(x => x is null || (x.Length <= 1000 && x.All(w => w is not null) && x.Select(w => w.SequenceNo).Distinct().Count() == x.Length))
            .WithMessage("Provide at most 1000 waypoints with unique sequences.");
        RuleForEach(x => x.Waypoints).SetValidator(new WaypointValidator());
        RuleFor(x => x).Must(x => x.ScheduledStartAt.HasValue == x.ScheduledEndAt.HasValue)
            .WithMessage("Both scheduledStartAt and scheduledEndAt are required together.");
        RuleFor(x => x).Must(x => !x.ScheduledStartAt.HasValue || x.ScheduledEndAt > x.ScheduledStartAt)
            .WithMessage("Scheduled end must be after scheduled start.");
    }
}
public sealed class ScheduleMissionValidator : AbstractValidator<ScheduleMissionRequest>
{
    public ScheduleMissionValidator()
    {
        RuleFor(x => x.ScheduledStartAt).NotEmpty();
        RuleFor(x => x.ScheduledEndAt).GreaterThan(x => x.ScheduledStartAt);
    }
}
public sealed class MissionActionValidator : AbstractValidator<MissionActionRequest>
{
    public MissionActionValidator()
    {
        RuleFor(x => x.OperatorNotes).MaximumLength(10000);
        RuleFor(x => x.FailureReason).MaximumLength(10000);
        RuleFor(x => x.Note).MaximumLength(10000);
        RuleFor(x => x.Reason).MaximumLength(10000);
    }
}
public sealed class MissionQueryValidator : AbstractValidator<MissionQuery>
{
    public MissionQueryValidator()
    {
        Include(new PageQueryValidator());
        RuleFor(x => x.FarmId).GreaterThan(0);
        RuleFor(x => x.UavId).GreaterThan(0);
        RuleFor(x => x.GatewayId).GreaterThan(0);
        RuleFor(x => x.Status).Must(x => x is null || Enum.GetNames<MissionStatus>().Contains(x)).WithMessage("Invalid mission status.");
        RuleFor(x => x).Must(x => !x.ScheduledFrom.HasValue || !x.ScheduledTo.HasValue || x.ScheduledTo >= x.ScheduledFrom)
            .WithMessage("Invalid scheduled date range.");
    }
}
