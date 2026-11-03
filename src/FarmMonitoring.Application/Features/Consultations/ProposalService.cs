using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Entities;
using FluentValidation;

namespace FarmMonitoring.Application.Features.Consultations;

public sealed class ProposalService(IProposalRepository repository, ConsultationService consultations, ICurrentUser current,
    IValidator<ProposalRequest> validator, IValidator<ProposalConfirmationRequest> confirmationValidator,
    IValidator<PageQuery> pages, TimeProvider clock)
{
    private int Actor => current.UserId ?? throw new AuthException("Authentication required.");
    public async Task<PagedResult<ProposalSummary>> ListAsync(int consultationId, PageQuery query, CancellationToken ct)
    {
        await consultations.EnsureRecordReadAsync(consultationId, ct);
        await pages.ValidateAndThrowAsync(query, ct);
        return await repository.ListAsync(consultationId, query, ct);
    }
    public async Task<ProposalResponse> GetAsync(int consultationId, int id, CancellationToken ct)
    {
        await consultations.EnsureRecordReadAsync(consultationId, ct);
        return Response(await repository.GetAsync(consultationId, id, ct) ?? throw new NotFoundException("Proposal not found."));
    }
    public async Task<ProposalResponse> CreateAsync(int consultationId, ProposalRequest request, CancellationToken ct)
    {
        var actor = Actor;
        await validator.ValidateAndThrowAsync(request, ct);
        var row = new DeviceProposal { ConsultationId = consultationId, SurveyId = request.SurveyId, CreatedByUserId = actor,
            RentalMonths = request.RentalMonths, RequestedInstallationAt = request.RequestedInstallationAt.ToUniversalTime(),
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(), CreatedAt = clock.GetUtcNow(),
            Lines = request.Lines.Select(x => new ProposalLine { SurveyZoneId = x.SurveyZoneId, DeviceType = x.DeviceType.Trim(),
                Quantity = x.Quantity, InstallationLocation = x.InstallationLocation?.Trim() ?? "", PublicContentId = x.PublicContentId }).ToList() };
        await repository.CreateAsync(row, request.ExpectedRevision, ct);
        return Response(row);
    }
    public async Task<ProposalResponse> ConfirmAsync(int consultationId, int id, bool deposit, ProposalConfirmationRequest request, CancellationToken ct)
    {
        var actor = Actor;
        await confirmationValidator.ValidateAndThrowAsync(request, ct);
        await repository.ConfirmAsync(consultationId, id, actor, deposit, request.Note.Trim(), clock.GetUtcNow(), ct);
        return await GetAsync(consultationId, id, ct);
    }
    public async Task<InstallationRequestResponse> SubmitAsync(int consultationId, int id, CancellationToken ct)
    {
        var row = await repository.SubmitAsync(consultationId, id, Actor, clock.GetUtcNow(), ct);
        return new(row.Id, row.ProposalId, row.FarmId, row.OwnerId, row.Status.ToString(), row.SubmittedAt);
    }
    private static ProposalResponse Response(DeviceProposal x) => new(x.Id, x.ConsultationId, x.SurveyId, x.Revision, x.RentalMonths,
        x.RequestedInstallationAt, x.Notes, x.Status.ToString(), x.CreatedByUserId, x.CreatedAt, x.PlanConfirmedByUserId,
        x.PlanConfirmedAt, x.PlanConfirmationNote, x.DepositConfirmedByUserId, x.DepositConfirmedAt, x.DepositConfirmationNote,
        x.Lines.OrderBy(l => l.Id).Select(l => new ProposalLineResponse(l.Id, l.SurveyZoneId, l.DeviceType, l.Quantity, l.InstallationLocation, l.PublicContentId, l.PriceRevision, l.MonthlyRentVnd, l.ProductName)).ToArray());
}
