using FarmMonitoring.Application.Features.TechnicalSupport;

namespace FarmMonitoring.UnitTests;
public sealed class MaintenanceScheduleTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 30, 0, 0, 0, TimeSpan.Zero);
    private static MaintenanceScheduleRequest Valid => new(null, null, "Quarterly inspection", 90, Now.AddDays(1), 7, true, 0, 12);

    [Fact]
    public void Requires_one_farm_scope_and_valid_recurrence()
    {
        var validator = new MaintenanceScheduleValidator(new FixedClock());
        Assert.True(validator.Validate(Valid).IsValid);
        Assert.False(validator.Validate(Valid with { FarmId = null }).IsValid);
        Assert.False(validator.Validate(Valid with { DeviceType = "UAV" }).IsValid);
        Assert.False(validator.Validate(Valid with { FarmId = 0 }).IsValid);
        Assert.False(validator.Validate(Valid with { RentalDeviceAssignmentId = 0 }).IsValid);
        Assert.False(validator.Validate(Valid with { RentalDeviceAssignmentId = null, DeviceType = "OTHER" }).IsValid);
        Assert.False(validator.Validate(Valid with { IntervalDays = 0 }).IsValid);
        Assert.False(validator.Validate(Valid with { IntervalDays = 3651 }).IsValid);
        Assert.False(validator.Validate(Valid with { TechnicianId = 0 }).IsValid);
        Assert.False(validator.Validate(Valid with { Policy = " " }).IsValid);
        Assert.False(validator.Validate(Valid with { Policy = new string('x', 2001) }).IsValid);
        Assert.False(validator.Validate(Valid with { ExpectedRevision = -1 }).IsValid);
        Assert.False(validator.Validate(Valid with { ExpectedRevision = int.MaxValue }).IsValid);
        Assert.False(validator.Validate(Valid with { NextDueAt = Now }).IsValid);
    }

    [Fact]
    public void Recurrence_retains_anchor_and_skips_missed_periods_without_drift()
    {
        Assert.Equal(Now.AddDays(7), MaintenanceRecurrence.After(Now, 7, Now));
        Assert.Equal(Now.AddDays(21), MaintenanceRecurrence.After(Now, 7, Now.AddDays(15).AddHours(3)));
        Assert.Equal(Now.AddDays(28), MaintenanceRecurrence.After(Now, 7, Now.AddDays(21)));
        Assert.Equal(Now, MaintenanceRecurrence.After(Now, 7, Now.AddDays(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => MaintenanceRecurrence.After(Now, 0, Now));
    }
    private sealed class FixedClock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
}
