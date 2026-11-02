using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Interfaces;
using FluentValidation;

namespace FarmMonitoring.Application.Features.Rentals;

public sealed record CreateRentalRequest(DateTimeOffset StartAt, DateTimeOffset EndAt, string Note);
public sealed record RentalActionRequest(int ExpectedRevision, string Note);
public sealed record ExtendRentalRequest(int ExpectedRevision, DateTimeOffset EndAt, string Note);
public sealed record EndRentalRequest(int ExpectedRevision, string Note, bool DevicesReturned);
public sealed record RentalSummary(int Id, int InstallationJobId, int FarmId, int OwnerId, DateTimeOffset StartAt, DateTimeOffset EndAt, string Status, int Revision);
public sealed record RentalDeviceResponse(int Id, int? SensorNodeId, int? UavId, int? GatewayId, string Code, string Name, string InstallationLocation, DateTimeOffset? ReturnedAt);
public sealed record RentalHistoryResponse(int Id, string Action, int? UserId, string Note, DateTimeOffset CreatedAt, DateTimeOffset StartAt, DateTimeOffset EndAt);
public sealed record RentalDetail(int Id, int InstallationJobId, int FarmId, int OwnerId, DateTimeOffset StartAt, DateTimeOffset EndAt, string Status, int Revision,
    IReadOnlyList<RentalDeviceResponse> Devices, IReadOnlyList<RentalHistoryResponse> History);
public sealed class CreateRentalValidator : AbstractValidator<CreateRentalRequest>
{
    public CreateRentalValidator(TimeProvider clock)
    {
        RuleFor(x => x.StartAt).NotEqual(default(DateTimeOffset));
        RuleFor(x => x.EndAt).GreaterThan(x => x.StartAt).Must(x => x > clock.GetUtcNow()).WithMessage("Rental end must be in the future and after its start.");
        RuleFor(x => x.Note).NotEmpty().MaximumLength(2000);
    }
}
public sealed class RentalActionValidator : AbstractValidator<RentalActionRequest>
{
    public RentalActionValidator() { RuleFor(x => x.ExpectedRevision).InclusiveBetween(1, int.MaxValue - 1); RuleFor(x => x.Note).NotEmpty().MaximumLength(2000); }
}
public sealed class ExtendRentalValidator : AbstractValidator<ExtendRentalRequest>
{
    public ExtendRentalValidator(TimeProvider clock)
    {
        RuleFor(x => x.ExpectedRevision).InclusiveBetween(1, int.MaxValue - 1); RuleFor(x => x.Note).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.EndAt).Must(x => x > clock.GetUtcNow()).WithMessage("Confirmed extension must end in the future.");
    }
}
public sealed class EndRentalValidator : AbstractValidator<EndRentalRequest>
{
    public EndRentalValidator()
    {
        RuleFor(x => x.ExpectedRevision).InclusiveBetween(1, int.MaxValue - 1); RuleFor(x => x.Note).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.DevicesReturned).Equal(true).WithMessage("Confirm retrieval of all assigned devices before ending the contract.");
    }
}
public sealed class RentalService(IRentalRepository repository, ICurrentUser current, TimeProvider clock, IValidator<PageQuery> pages,
    IValidator<CreateRentalRequest> creates, IValidator<RentalActionRequest> actions, IValidator<ExtendRentalRequest> extensions, IValidator<EndRentalRequest> endings)
{
    private int Actor => current.UserId ?? throw new AuthException("Authentication required.");
    public async Task<PagedResult<RentalSummary>> ListAsync(PageQuery query, CancellationToken ct)
    { await pages.ValidateAndThrowAsync(query, ct); return await repository.ListAsync(Actor, query, clock.GetUtcNow(), ct); }
    public Task<RentalDetail> GetAsync(int id, CancellationToken ct) => repository.GetAsync(id, Actor, clock.GetUtcNow(), ct);
    public async Task<RentalDetail> CreateAsync(int jobId, CreateRentalRequest request, CancellationToken ct)
    {
        await creates.ValidateAndThrowAsync(request, ct);
        var id = await repository.CreateAsync(jobId, Actor, request with { StartAt = request.StartAt.ToUniversalTime(), EndAt = request.EndAt.ToUniversalTime() }, clock.GetUtcNow(), ct);
        return await GetAsync(id, ct);
    }
    public async Task<RentalDetail> ActivateAsync(int id, RentalActionRequest request, CancellationToken ct)
    { await actions.ValidateAndThrowAsync(request, ct); await repository.ChangeAsync(id, Actor, "ACTIVATED", request.ExpectedRevision, request.Note, null, clock.GetUtcNow(), ct); return await GetAsync(id, ct); }
    public async Task<RentalDetail> ExtendAsync(int id, ExtendRentalRequest request, CancellationToken ct)
    { await extensions.ValidateAndThrowAsync(request, ct); await repository.ChangeAsync(id, Actor, "EXTENDED", request.ExpectedRevision, request.Note, request.EndAt.ToUniversalTime(), clock.GetUtcNow(), ct); return await GetAsync(id, ct); }
    public async Task<RentalDetail> EndAsync(int id, EndRentalRequest request, CancellationToken ct)
    { await endings.ValidateAndThrowAsync(request, ct); await repository.ChangeAsync(id, Actor, "ENDED", request.ExpectedRevision, request.Note, null, clock.GetUtcNow(), ct); return await GetAsync(id, ct); }
}
public sealed class RentalExpirationSettings
{
    public bool Enabled { get; set; } = true;
    public int IntervalSeconds { get; set; } = 3600;
    public int WarningDays { get; set; } = 7;
    public int BatchSize { get; set; } = 100;
}
public sealed class RentalExpirationService(IRentalRepository repository, TimeProvider clock, RentalExpirationSettings settings)
{
    public Task RunOnceAsync(CancellationToken ct) => repository.MonitorAsync(clock.GetUtcNow(), settings.WarningDays, settings.BatchSize, ct);
}
