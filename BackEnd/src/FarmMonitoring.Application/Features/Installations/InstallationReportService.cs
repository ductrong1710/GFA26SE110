using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Entities;
using FluentValidation;
using FarmMonitoring.Application.Features.TechnicalSupport;

namespace FarmMonitoring.Application.Features.Installations;

public sealed record InstalledDeviceRequest(int ProposalLineId, int? SensorNodeId, int? UavId, int? GatewayId,
    string InstallationLocation, bool? AccessoriesChecked, bool? ConnectivityPassed, bool? OperationPassed, string TestResults);
public sealed record InstallationReportRequest(int ExpectedRevision, string Notes, string? Issues, InstalledDeviceRequest[] Devices, EvidenceImageData[]? Images = null,
    PaperInstallation? Paperwork = null, DateTimeOffset? CompletedAt = null);
public sealed record ReportDecisionRequest(int ExpectedRevision, int ReportId, string Note);
public sealed record ReportSummary(int Id, int Revision, int TechnicianId, DateTimeOffset CreatedAt, bool? Approved);
public sealed record InstallationReportResponse(int Id, int JobId, int Revision, int TechnicianId, string Notes, string? Issues,
    DateTimeOffset CreatedAt, int? ConfirmedByUserId, DateTimeOffset? ConfirmedAt, string? ConfirmationNote,
    int? ReviewedByUserId, DateTimeOffset? ReviewedAt, string? ReviewNote, bool? Approved, IReadOnlyList<InstalledDeviceRequest> Devices, IReadOnlyList<EvidenceImageData> Images,
    string? PaperworkJson = null, DateTimeOffset? CompletedAt = null, DateTimeOffset? SuggestedRentalStartAt = null);
public sealed class InstallationReportValidator : AbstractValidator<InstallationReportRequest>
{
    public InstallationReportValidator()
    {
        RuleFor(x => x.ExpectedRevision).InclusiveBetween(1, int.MaxValue - 1);
        RuleFor(x => x.Notes).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.Issues).MaximumLength(4000);
        RuleFor(x => x.Devices).NotNull().Must(x => x is { Length: > 0 and <= 1000 }).WithMessage("Provide 1 to 1000 installed device checks.");
        RuleForEach(x => x.Devices).NotNull().SetValidator(new InstalledDeviceValidator());
        RuleFor(x => x).Must(x => x.Devices is null || x.Devices.All(d => d is null || d.AccessoriesChecked == true && d.ConnectivityPassed == true && d.OperationPassed == true) || !string.IsNullOrWhiteSpace(x.Issues))
            .WithMessage("Describe issues when a device check fails.");
    }
}
public sealed class InstalledDeviceValidator : AbstractValidator<InstalledDeviceRequest>
{
    public InstalledDeviceValidator()
    {
        RuleFor(x => x.ProposalLineId).GreaterThan(0);
        RuleFor(x => x).Must(x => new[] { x.SensorNodeId, x.UavId, x.GatewayId }.Count(id => id.HasValue) == 1 && new[] { x.SensorNodeId, x.UavId, x.GatewayId }.All(id => id is null or > 0))
            .WithMessage("Select exactly one registered device.");
        RuleFor(x => x.InstallationLocation).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.AccessoriesChecked).NotNull(); RuleFor(x => x.ConnectivityPassed).NotNull(); RuleFor(x => x.OperationPassed).NotNull();
        RuleFor(x => x.TestResults).NotEmpty().MaximumLength(2000);
    }
}
public sealed class InstallationReportService(IInstallationReportRepository repository, InstallationJobService jobs,
    ICurrentUser current, IValidator<InstallationReportRequest> validator, IValidator<PageQuery> pages, TimeProvider clock)
{
    private int Actor => current.UserId ?? throw new AuthException("Authentication required.");
    public async Task<PagedResult<ReportSummary>> ListAsync(int jobId, PageQuery query, CancellationToken ct)
    {
        await jobs.GetAsync(jobId, ct); await pages.ValidateAndThrowAsync(query, ct);
        return await repository.ListAsync(jobId, query, ct);
    }
    public async Task<InstallationReportResponse> GetAsync(int jobId, int id, CancellationToken ct)
    {
        await jobs.GetAsync(jobId, ct);
        return Response(await repository.GetAsync(jobId, id, ct) ?? throw new NotFoundException("Installation report not found."));
    }
    public async Task<InstallationReportResponse> SubmitAsync(int jobId, InstallationReportRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var row = await repository.SubmitAsync(jobId, Actor, request, clock.GetUtcNow(), ct);
        return Response(row);
    }
    public async Task<JobDetail> DecideAsync(int jobId, string action, ReportDecisionRequest request, CancellationToken ct)
    {
        if (request.ExpectedRevision <= 0 || request.ExpectedRevision == int.MaxValue || request.ReportId <= 0 || string.IsNullOrWhiteSpace(request.Note) || request.Note.Length > 2000)
            throw new ValidationException("Valid revision, reportId and a note up to 2000 characters are required.");
        await repository.DecideAsync(jobId, Actor, action, request, clock.GetUtcNow(), ct);
        return await jobs.GetAsync(jobId, ct);
    }
    private static InstallationReportResponse Response(InstallationReport x) => new(x.Id, x.JobId, x.Revision, x.TechnicianId, x.Notes, x.Issues, x.CreatedAt,
        x.ConfirmedByUserId, x.ConfirmedAt, x.ConfirmationNote, x.ReviewedByUserId, x.ReviewedAt, x.ReviewNote, x.Approved,
        x.Devices.OrderBy(d => d.Id).Select(d => new InstalledDeviceRequest(d.ProposalLineId, d.SensorNodeId, d.UavId, d.GatewayId, d.InstallationLocation,
            d.AccessoriesChecked, d.ConnectivityPassed, d.OperationPassed, d.TestResults)).ToArray(), EvidenceImages.Encode(x.Images), x.PaperworkJson, x.CompletedAt, OperationalRules.RentalStart(x.CompletedAt ?? x.CreatedAt));
}
