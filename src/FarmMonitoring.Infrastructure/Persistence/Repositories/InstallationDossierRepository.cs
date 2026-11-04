using System.Text.Json;
using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Consultations;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Constants;
using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FarmMonitoring.Infrastructure.Persistence.Repositories;

public sealed class InstallationDossierRepository(AppDbContext db, DossierCustomerProvisioner customers) : IInstallationDossierRepository
{
    public async Task<PagedResult<DossierSummary>> ListAsync(int actor, PageQuery query, CancellationToken ct)
    {
        await RequireRoleAsync(actor, RoleNames.FarmAdministrator, ct);
        var rows = db.DeviceProposals.AsNoTracking().Where(x => x.Status == ProposalStatus.WAITING_ADMIN);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            rows = rows.Where(x => x.Consultation.CustomerName.Contains(search) || x.Consultation.Phone.Contains(search) || x.Survey.FarmName.Contains(search));
        }
        var total = await rows.CountAsync(ct);
        var items = await rows.OrderBy(x => x.DossierSubmittedAt).ThenBy(x => x.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new DossierSummary(x.Id, x.ConsultationId, x.Revision, x.Consultation.CustomerName,
                x.Consultation.Phone, x.Survey.FarmName, x.Survey.FarmLocation, x.DossierSubmittedAt)).ToArrayAsync(ct);
        return new(items, total, query.Page, query.PageSize);
    }
    public async Task<DossierDecision> SubmitAsync(int id, int actor, int revision, string note,
        EvidenceImageData[] bills, DossierCustomerRequest? customer, DateTimeOffset now, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LockActorAsync(actor, RoleNames.FarmEngineer, ct);
        var (proposal, consultation) = await LockProposalAsync(id, revision, ct);
        if (consultation.EngineerId != actor) throw new AccessDeniedException("Assigned engineer access required.");
        if (proposal.Status != ProposalStatus.DEPOSIT_CONFIRMED)
            throw new ConflictException("Only a proposal with a confirmed plan and deposit can be submitted.");
        if (proposal.RequestedInstallationAt <= now) throw new BusinessRuleException("Requested installation time has passed. Revise the dossier.");
        if (!await db.Set<ProposalLine>().AnyAsync(x => x.ProposalId == id, ct)) throw new BusinessRuleException("Device information is required.");
        if (await db.InstallationRequests.AnyAsync(x => x.Proposal.ConsultationId == consultation.Id && x.Status != InstallationRequestStatus.RETURNED, ct))
            throw new ConflictException("Installation has already been submitted.");
        await customers.EnsureAsync(consultation, proposal.SurveyId, customer, now, ct);
        proposal.DepositBillsJson = JsonSerializer.Serialize(bills); proposal.DossierSubmittedAt = now;
        proposal.DossierReviewNote = null; proposal.Status = ProposalStatus.WAITING_ADMIN;
        History(consultation, actor, ConsultationAction.INSTALLATION_SUBMITTED, $"Dossier #{id} submitted with external deposit bill: {note}", now);
        await WorkflowNotifications.AddForRoleAsync(db, RoleNames.FarmAdministrator, "Installation dossier review required",
            $"Consultation #{consultation.Id}, dossier #{id}: review farm information and deposit bill before assigning a technician.", now, ct);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return new(id, proposal.Status.ToString(), null);
    }
    public async Task<DossierDecision> ReviewAsync(int id, int actor, DossierReviewRequest request, DateTimeOffset now, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LockActorAsync(actor, RoleNames.FarmAdministrator, ct);
        var (proposal, consultation) = await LockProposalAsync(id, request.ExpectedRevision, ct);
        if (proposal.Status != ProposalStatus.WAITING_ADMIN) throw new ConflictException("Only a pending dossier can be reviewed.");
        InstallationRequest? installation = null;
        if (request.Approve)
        {
            if (EvidenceImages.Decode(JsonSerializer.Deserialize<EvidenceImageData[]>(proposal.DepositBillsJson ?? "[]")).Count == 0)
                throw new BusinessRuleException("Deposit bill is required before approval.");
            if (consultation.OwnerId is not int owner || consultation.FarmId is not int farm ||
                !await FarmAccessQuery.AssignedFarmIds(db, owner).ContainsAsync(farm, ct) ||
                !await db.Farms.AnyAsync(x => x.Id == farm && x.IsActive, ct))
                throw new BusinessRuleException("Link an active customer and authorized farm before approval.");
            if (proposal.RequestedInstallationAt <= now) throw new BusinessRuleException("Requested schedule has passed. Return the dossier for correction.");
            if (await db.InstallationRequests.AnyAsync(x => x.Proposal.ConsultationId == consultation.Id && x.Status != InstallationRequestStatus.RETURNED, ct))
                throw new ConflictException("Installation has already been approved or submitted.");
            installation = new InstallationRequest { ProposalId = id, FarmId = farm, OwnerId = owner,
                SubmittedByUserId = proposal.CreatedByUserId, SubmittedAt = proposal.DossierSubmittedAt!.Value,
                Status = InstallationRequestStatus.APPROVED, ReviewedAt = now, ReviewedByUserId = actor, ReviewNote = request.Note.Trim() };
            db.InstallationRequests.Add(installation); proposal.Status = ProposalStatus.SUBMITTED;
        }
        else proposal.Status = ProposalStatus.RETURNED;
        proposal.DossierReviewNote = request.Note.Trim();
        History(consultation, actor, request.Approve ? ConsultationAction.INSTALLATION_APPROVED : ConsultationAction.INSTALLATION_RETURNED,
            $"Dossier #{id}: {request.Note.Trim()}", now);
        await WorkflowNotifications.AddForUserAsync(db, proposal.CreatedByUserId,
            request.Approve ? "Installation dossier approved" : "Installation dossier correction required",
            $"Consultation #{consultation.Id}, dossier #{id}: {request.Note.Trim()}", now, ct);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return new(id, proposal.Status.ToString(), installation?.Id);
    }
    private async Task RequireRoleAsync(int actor, string role, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(x => x.Id == actor && x.IsActive && x.UserRoles.Any(r => r.Role.Name == role), ct))
            throw new AccessDeniedException("Access denied.");
    }
    private async Task LockActorAsync(int actor, string role, CancellationToken ct)
    {
        await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id = {actor} FOR UPDATE").SingleOrDefaultAsync(ct);
        await RequireRoleAsync(actor, role, ct);
    }
    private async Task<(DeviceProposal, Consultation)> LockProposalAsync(int id, int revision, CancellationToken ct)
    {
        var consultationId = await db.DeviceProposals.Where(x => x.Id == id).Select(x => (int?)x.ConsultationId).SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException("Dossier not found.");
        var consultation = await db.Consultations.FromSqlInterpolated($"SELECT * FROM consultations WHERE id = {consultationId} FOR UPDATE").SingleAsync(ct);
        var proposal = await db.DeviceProposals.SingleAsync(x => x.Id == id, ct);
        if (consultation.Status != ConsultationStatus.IN_PROGRESS) throw new ConflictException("Consultation is not in progress.");
        if (proposal.Revision != revision || await db.DeviceProposals.AnyAsync(x => x.ConsultationId == consultationId && x.Revision > revision, ct))
            throw new ConflictException("Dossier changed. Reload the latest revision.");
        var survey = await db.FarmSurveys.Where(x => x.ConsultationId == consultationId).OrderByDescending(x => x.Revision).Select(x => x.Id).FirstAsync(ct);
        if (proposal.SurveyId != survey) throw new ConflictException("Farm information changed. Revise the dossier.");
        return (proposal, consultation);
    }
    private static void History(Consultation row, int actor, ConsultationAction action, string note, DateTimeOffset now)
    {
        row.UpdatedAt = now;
        row.History.Add(new() { UserId = actor, Action = action, Notes = note, Status = row.Status, CreatedAt = now });
    }
}
