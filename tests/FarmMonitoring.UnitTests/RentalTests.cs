using FarmMonitoring.Application.Features.Rentals;
using FarmMonitoring.Domain.Entities;

namespace FarmMonitoring.UnitTests;

public class RentalTests
{
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => new(2026, 10, 29, 0, 0, 0, TimeSpan.Zero); }
    [Fact]
    public void Contract_dates_and_evidence_are_required()
    {
        var clock = new Clock(); var validator = new CreateRentalValidator(clock);
        var request = new CreateRentalRequest(clock.GetUtcNow(), clock.GetUtcNow().AddMonths(1), "Agreement confirmed");
        Assert.True(validator.Validate(request).IsValid);
        Assert.False(validator.Validate(request with { EndAt = request.StartAt }).IsValid);
        Assert.False(validator.Validate(request with { StartAt = default }).IsValid);
        Assert.False(validator.Validate(request with { Note = " " }).IsValid);
        Assert.False(validator.Validate(request with { StartAt = request.StartAt.AddDays(-2), EndAt = request.StartAt.AddDays(-1) }).IsValid);
    }
    [Fact]
    public void Changes_require_current_revision_and_explicit_return_confirmation()
    {
        var validator = new EndRentalValidator();
        var request = new EndRentalRequest(1, "All devices collected", true);
        Assert.True(validator.Validate(request).IsValid);
        Assert.False(validator.Validate(request with { DevicesReturned = false }).IsValid);
        Assert.False(validator.Validate(request with { ExpectedRevision = 0 }).IsValid);
        Assert.False(validator.Validate(request with { ExpectedRevision = int.MaxValue }).IsValid);
        Assert.False(validator.Validate(request with { Note = "" }).IsValid);
    }
    [Fact]
    public void Expiration_is_effective_at_boundary_even_before_worker_runs()
    {
        var now = new Clock().GetUtcNow();
        var rental = new RentalContract { Status = RentalStatus.ACTIVE, StartAt = now.AddDays(-1), EndAt = now };
        Assert.Equal(RentalStatus.EXPIRED, rental.EffectiveStatus(now));
        Assert.Equal(RentalStatus.ACTIVE, rental.EffectiveStatus(now.AddTicks(-1)));
        rental.Status = RentalStatus.ENDED;
        Assert.Equal(RentalStatus.ENDED, rental.EffectiveStatus(now));
    }
}
