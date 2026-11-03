using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Consultations;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Constants;
using FarmMonitoring.Domain.Entities;
using FluentValidation;

namespace FarmMonitoring.Application.Features.Installations;

public sealed record InstallationReviewRequest(string Note);
public sealed record InstallationSummary(int Id, int ConsultationId, int ProposalId, int FarmId, int OwnerId, string Status, DateTimeOffset SubmittedAt);
public sealed record InstallationDetail(int Id, int ConsultationId, int ProposalId, int FarmId, int OwnerId, string Status,
    DateTimeOffset SubmittedAt, int RentalMonths, DateTimeOffset RequestedInstallationAt, string? PlanConfirmationNote,
    string? DepositConfirmationNote, int? ReviewedByUserId, DateTimeOffset? ReviewedAt, string? ReviewNote, IReadOnlyList<ProposalLineResponse> Lines,
    IReadOnlyList<EvidenceImageData> DepositBills);

public sealed class InstallationReviewValidator : AbstractValidator<InstallationReviewRequest>
{
    public InstallationReviewValidator() => RuleFor(x => x.Note).NotEmpty().MaximumLength(2000);
}

public sealed class InstallationReviewService(IInstallationRepository repository, ICurrentUser current, IAuthRepository users,
    ConsultationService consultations, IValidator<PageQuery> pages, IValidator<InstallationReviewRequest> validator, TimeProvider clock)
{
    private int Actor => current.UserId ?? throw new AuthException("Authentication required.");
    public async Task<PagedResult<InstallationSummary>> ListAsync(PageQuery query, CancellationToken ct)
    {
        var id = Actor;
        var user = await users.GetUserByIdAsync(id, ct);
        if (user is not { IsActive: true }) throw new AccessDeniedException("Access denied.");
        await pages.ValidateAndThrowAsync(query, ct);
        return await repository.ListAsync(id, user.UserRoles.Any(x => x.Role.Name == RoleNames.FarmAdministrator),
            user.UserRoles.Any(x => x.Role.Name == RoleNames.FarmEngineer), query, ct);
    }
    public async Task<InstallationDetail> GetAsync(int id, CancellationToken ct)
    {
        var row = await repository.GetAsync(id, ct) ?? throw new NotFoundException("Installation request not found.");
        await consultations.EnsureRecordReadAsync(row.Proposal.ConsultationId, ct);
        return Response(row);
    }
    public async Task<InstallationDetail> ReviewAsync(int id, bool approve, InstallationReviewRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        await repository.ReviewAsync(id, Actor, approve, request.Note.Trim(), clock.GetUtcNow(), ct);
        return await GetAsync(id, ct);
    }
    private static InstallationDetail Response(InstallationRequest row) => new(row.Id, row.Proposal.ConsultationId, row.ProposalId,
        row.FarmId, row.OwnerId, row.Status.ToString(), row.SubmittedAt, row.Proposal.RentalMonths, row.Proposal.RequestedInstallationAt,
        row.Proposal.PlanConfirmationNote, row.Proposal.DepositConfirmationNote, row.ReviewedByUserId, row.ReviewedAt, row.ReviewNote,
        row.Proposal.Lines.OrderBy(x => x.Id).Select(x => new ProposalLineResponse(x.Id, x.SurveyZoneId, x.DeviceType, x.Quantity, x.InstallationLocation, x.PublicContentId, x.PriceRevision, x.MonthlyRentVnd, x.ProductName)).ToArray(),
        System.Text.Json.JsonSerializer.Deserialize<EvidenceImageData[]>(row.Proposal.DepositBillsJson ?? "[]") ?? []);
}
