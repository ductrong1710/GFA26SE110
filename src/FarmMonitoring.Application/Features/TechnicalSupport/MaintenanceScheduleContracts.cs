using FluentValidation;

namespace FarmMonitoring.Application.Features.TechnicalSupport;

public sealed record MaintenanceScheduleRequest(int? RentalDeviceAssignmentId, string? DeviceType, string Policy,
    int IntervalDays, DateTimeOffset NextDueAt, int TechnicianId, bool Enabled, int ExpectedRevision);

public sealed class MaintenanceScheduleValidator : AbstractValidator<MaintenanceScheduleRequest>
{
    public MaintenanceScheduleValidator(TimeProvider clock)
    {
        RuleFor(x => x).Must(x => x.RentalDeviceAssignmentId.HasValue != (x.DeviceType is not null))
            .WithMessage("Choose either a rental device assignment or a device type.");
        RuleFor(x => x.RentalDeviceAssignmentId).GreaterThan(0).When(x => x.RentalDeviceAssignmentId.HasValue);
        RuleFor(x => x.DeviceType).Must(x => x is "SENSOR" or "GATEWAY" or "UAV").When(x => x.DeviceType is not null);
        RuleFor(x => x.Policy).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.IntervalDays).InclusiveBetween(1, 3650);
        RuleFor(x => x.NextDueAt).Must(x => x > clock.GetUtcNow() && x < DateTimeOffset.MaxValue.AddDays(-3650))
            .WithMessage("Choose a future maintenance date within the supported calendar range.");
        RuleFor(x => x.TechnicianId).GreaterThan(0);
        RuleFor(x => x.ExpectedRevision).InclusiveBetween(0, int.MaxValue - 1);
    }
}

public static class MaintenanceRecurrence
{
    public static DateTimeOffset After(DateTimeOffset dueAt, int intervalDays, DateTimeOffset now)
    {
        if (intervalDays is < 1 or > 3650) throw new ArgumentOutOfRangeException(nameof(intervalDays));
        if (dueAt > now) return dueAt;
        var periods = (now.UtcTicks - dueAt.UtcTicks) / TimeSpan.FromDays(intervalDays).Ticks + 1;
        return dueAt.AddTicks(checked(periods * TimeSpan.FromDays(intervalDays).Ticks));
    }
}
