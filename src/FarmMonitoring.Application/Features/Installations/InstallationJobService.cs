using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Consultations;
using FarmMonitoring.Application.Interfaces;
using FluentValidation;

namespace FarmMonitoring.Application.Features.Installations;

public sealed record JobAssignmentRequest(int TechnicianId, DateTimeOffset ScheduledAt, string Note, int ExpectedRevision = 0, DateTimeOffset? ScheduledEndAt = null);
public sealed record JobStartRequest(int ExpectedRevision);
public sealed record TechnicianOption(int Id, string FullName);
public sealed record JobSummary(int Id, int InstallationRequestId, int FarmId, int TechnicianId, DateTimeOffset ScheduledAt, string Status, int Revision);
public sealed record JobHistoryResponse(int Id, string Action, int UserId, int TechnicianId, DateTimeOffset ScheduledAt, string Note, DateTimeOffset CreatedAt);
public sealed record JobDetail(int Id, int InstallationRequestId, int FarmId, int TechnicianId, DateTimeOffset ScheduledAt, string Status, int Revision,
    string FarmName, string FarmLocation, IReadOnlyList<ProposalLineResponse> Lines, IReadOnlyList<JobHistoryResponse> History);
public sealed class JobAssignmentValidator : AbstractValidator<JobAssignmentRequest>
{
    public JobAssignmentValidator(TimeProvider clock)
    {
        RuleFor(x => x.TechnicianId).GreaterThan(0);
        RuleFor(x => x).Must(x => !x.ScheduledEndAt.HasValue || x.ScheduledEndAt > x.ScheduledAt && x.ScheduledEndAt <= x.ScheduledAt.AddHours(24)).WithMessage("End must follow start within 24 hours.");
        RuleFor(x => x.ScheduledAt).Must(x => x > clock.GetUtcNow()).WithMessage("Appointment must be in the future.");
        RuleFor(x => x.Note).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.ExpectedRevision).InclusiveBetween(0, int.MaxValue - 1);
    }
}
public sealed class InstallationJobService(IInstallationJobRepository repository, ICurrentUser current,
    IValidator<JobAssignmentRequest> validator, IValidator<PageQuery> pages, TimeProvider clock)
{
    private int Actor => current.UserId ?? throw new AuthException("Authentication required.");
    public async Task<PagedResult<JobSummary>> ListAsync(PageQuery query, CancellationToken ct)
    {
        await pages.ValidateAndThrowAsync(query, ct);
        return await repository.ListAsync(Actor, query, ct);
    }
    public async Task<PagedResult<TechnicianOption>> TechniciansAsync(PageQuery query, CancellationToken ct)
    {
        await pages.ValidateAndThrowAsync(query, ct);
        return await repository.TechniciansAsync(Actor, query, ct);
    }
    public Task<JobDetail> GetAsync(int id, CancellationToken ct) => repository.GetAsync(id, Actor, ct);
    public async Task<JobDetail> AssignAsync(int id, bool create, JobAssignmentRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var jobId = await repository.AssignAsync(id, create, Actor, request, clock.GetUtcNow(), ct);
        return await GetAsync(jobId, ct);
    }
    public async Task<JobDetail> StartAsync(int id, JobStartRequest request, CancellationToken ct)
    {
        if (request.ExpectedRevision <= 0 || request.ExpectedRevision == int.MaxValue) throw new ValidationException("A valid expectedRevision is required.");
        await repository.StartAsync(id, Actor, request.ExpectedRevision, clock.GetUtcNow(), ct);
        return await GetAsync(id, ct);
    }
}
