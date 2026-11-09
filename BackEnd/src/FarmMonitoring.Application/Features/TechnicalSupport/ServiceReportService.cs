using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Entities;
using FluentValidation;

namespace FarmMonitoring.Application.Features.TechnicalSupport;

public sealed record ServiceReportRequest(int ExpectedRevision, string Cause, string Work, string Components, string TestResults, string Notes,
    bool? ConnectivityPassed, bool? OperationPassed, EvidenceImageData[]? Images = null, EvidenceImageData[]? SignedHandover = null);
public sealed record ServiceReportDecisionRequest(int ExpectedRevision, int ReportId, string Note);
public sealed record ServiceReportSummary(int Id, int Revision, int TechnicianId, DateTimeOffset CreatedAt, bool? Approved);
public sealed record ServiceReportResponse(int Id, int JobId, int Revision, int TechnicianId, string Cause, string Work, string Components,
    string TestResults, string Notes, bool ConnectivityPassed, bool OperationPassed, DateTimeOffset CreatedAt,
    int? ConfirmedByUserId, DateTimeOffset? ConfirmedAt, string? ConfirmationNote, int? ReviewedByUserId, DateTimeOffset? ReviewedAt,
    string? ReviewNote, bool? Approved, IReadOnlyList<EvidenceImageData> Images, ServiceReplacementResponse? Replacement, string? PaperworkJson = null);
public sealed class ServiceReportValidator : AbstractValidator<ServiceReportRequest>
{
    public ServiceReportValidator()
    {
        RuleFor(x => x.ExpectedRevision).InclusiveBetween(1, int.MaxValue - 1);
        RuleFor(x => x.Cause).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.Work).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.Components).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.Notes).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.TestResults).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.ConnectivityPassed).NotNull();
        RuleFor(x => x.OperationPassed).NotNull();
    }
}
public sealed class ServiceReportService(IServiceReportRepository repository, ServiceJobService jobs, ICurrentUser current,
    IValidator<ServiceReportRequest> validator, IValidator<PageQuery> pages, TimeProvider clock)
{
    private int Actor => current.UserId ?? throw new AuthException("Authentication required.");
    public async Task<PagedResult<ServiceReportSummary>> ListAsync(int jobId, PageQuery query, CancellationToken ct)
    {
        await jobs.GetAsync(jobId, ct);
        await pages.ValidateAndThrowAsync(query, ct);
        return await repository.ListAsync(jobId, query, ct);
    }
    public async Task<ServiceReportResponse> GetAsync(int jobId, int id, CancellationToken ct)
    {
        await jobs.GetAsync(jobId, ct);
        return Response(await repository.GetAsync(jobId, id, ct) ?? throw new NotFoundException("Service report not found."));
    }
    public async Task<ServiceReportResponse> SubmitAsync(int jobId, ServiceReportRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        return Response(await repository.SubmitAsync(jobId, Actor, request, clock.GetUtcNow(), ct));
    }
    public async Task<ServiceJobDetail> DecideAsync(int jobId, string action, ServiceReportDecisionRequest request, CancellationToken ct)
    {
        if (request.ExpectedRevision <= 0 || request.ExpectedRevision == int.MaxValue || request.ReportId <= 0 || string.IsNullOrWhiteSpace(request.Note) || request.Note.Length > 2000)
            throw new ValidationException("Valid revision, reportId and a note up to 2000 characters are required.");
        await repository.DecideAsync(jobId, Actor, action, request, clock.GetUtcNow(), ct);
        return await jobs.GetAsync(jobId, ct);
    }
    private static ServiceReportResponse Response(ServiceReport x) => new(x.Id, x.JobId, x.Revision, x.TechnicianId, x.Cause, x.Work, x.Components,
        x.TestResults, x.Notes, x.ConnectivityPassed, x.OperationPassed, x.CreatedAt, x.ConfirmedByUserId, x.ConfirmedAt, x.ConfirmationNote,
        x.ReviewedByUserId, x.ReviewedAt, x.ReviewNote, x.Approved, EvidenceImages.Encode(x.Images), x.Replacement is null ? null : ServiceReplacementResponse.From(x.Replacement), x.PaperworkJson);
}
