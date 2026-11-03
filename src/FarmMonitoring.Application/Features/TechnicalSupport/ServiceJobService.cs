using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Interfaces;
using FluentValidation;

namespace FarmMonitoring.Application.Features.TechnicalSupport;

public sealed record ServiceJobAssignmentRequest(string Type, int TechnicianId, DateTimeOffset ScheduledAt, string Note, int ExpectedRevision = 0, DateTimeOffset? ScheduledEndAt = null, int? RentalDeviceAssignmentId = null);
public sealed record ServiceJobStartRequest(int ExpectedRevision);
public sealed record ServiceJobSummary(int Id, int? IncidentId, int FarmId, string DeviceCode, string Type, int TechnicianId, DateTimeOffset ScheduledAt, string Status, int Revision);
public sealed record ServiceHistoryResponse(int Id, string Action, int UserId, int TechnicianId, DateTimeOffset ScheduledAt, string Note, DateTimeOffset CreatedAt);
public sealed record ServiceJobDetail(int Id, int? IncidentId, int FarmId, string DeviceCode, string Type, int TechnicianId, DateTimeOffset ScheduledAt, string Status, int Revision,
    string Description, string InstallationLocation, EvidenceImageData[] Images, ServiceHistoryResponse[] History, string CurrentDeviceCode, ServiceReplacementResponse[] Replacements);
public sealed class ServiceJobAssignmentValidator : AbstractValidator<ServiceJobAssignmentRequest>
{
    public ServiceJobAssignmentValidator(TimeProvider clock)
    {
        RuleFor(x => x.Type).Must(x => x is "MAINTENANCE" or "REPAIR");
        RuleFor(x => x.TechnicianId).GreaterThan(0);
        RuleFor(x => x).Must(x => !x.ScheduledEndAt.HasValue || x.ScheduledEndAt > x.ScheduledAt && x.ScheduledEndAt <= x.ScheduledAt.AddHours(24)).WithMessage("End must follow start within 24 hours.");
        RuleFor(x => x.ScheduledAt).Must(x => x > clock.GetUtcNow()).WithMessage("Choose a future appointment.");
        RuleFor(x => x.Note).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.ExpectedRevision).InclusiveBetween(0, int.MaxValue - 1);
    }
}
public sealed class ServiceJobService(IServiceJobRepository repository, ICurrentUser current, TimeProvider clock,
    IValidator<PageQuery> pages, IValidator<ServiceJobAssignmentRequest> assignments)
{
    private int Actor => current.UserId ?? throw new AuthException("Authentication required.");
    public async Task<PagedResult<ServiceJobSummary>> ListAsync(PageQuery query, CancellationToken ct)
    {
        await pages.ValidateAndThrowAsync(query, ct);
        return await repository.ListAsync(Actor, query, ct);
    }
    public Task<ServiceJobDetail> GetAsync(int id, CancellationToken ct) => repository.GetAsync(id, Actor, ct);
    public async Task<ServiceJobDetail> AssignAsync(int id, bool create, ServiceJobAssignmentRequest request, CancellationToken ct)
    {
        await assignments.ValidateAndThrowAsync(request, ct);
        var jobId = await repository.AssignAsync(id, create, Actor, request, clock.GetUtcNow(), ct);
        return await GetAsync(jobId, ct);
    }
    public async Task<ServiceJobDetail> StartAsync(int id, ServiceJobStartRequest request, CancellationToken ct)
    {
        if (request.ExpectedRevision <= 0 || request.ExpectedRevision == int.MaxValue) throw new ValidationException("A valid expectedRevision is required.");
        await repository.StartAsync(id, Actor, request.ExpectedRevision, clock.GetUtcNow(), ct);
        return await GetAsync(id, ct);
    }
}
