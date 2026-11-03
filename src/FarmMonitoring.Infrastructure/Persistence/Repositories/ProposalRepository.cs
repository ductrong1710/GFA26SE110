using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Consultations;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Constants;
using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FarmMonitoring.Infrastructure.Persistence.Repositories;

public sealed class ProposalRepository(AppDbContext db) : IProposalRepository
{
    public async Task<PagedResult<ProposalSummary>> ListAsync(int consultationId, PageQuery query, CancellationToken ct)
    {
        var rows = db.DeviceProposals.AsNoTracking().Where(x => x.ConsultationId == consultationId);
        var total = await rows.CountAsync(ct);
        var items = await rows.OrderByDescending(x => x.Revision).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new ProposalSummary(x.Id, x.Revision, x.SurveyId, x.Status.ToString(), x.CreatedAt)).ToArrayAsync(ct);
        return new(items, total, query.Page, query.PageSize);
    }
    public Task<DeviceProposal?> GetAsync(int consultationId, int id, CancellationToken ct) =>
        db.DeviceProposals.AsNoTracking().Include(x => x.Lines).SingleOrDefaultAsync(x => x.ConsultationId == consultationId && x.Id == id, ct);

    public async Task CreateAsync(DeviceProposal proposal, int expectedRevision, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var consultation = await LockWorkAsync(proposal.ConsultationId, proposal.CreatedByUserId, false, ct);
        var latest = await db.DeviceProposals.Where(x => x.ConsultationId == proposal.ConsultationId).MaxAsync(x => (int?)x.Revision, ct) ?? 0;
        if (latest != expectedRevision) throw new ConflictException("Proposal changed. Reload the latest revision.");
        if (await db.InstallationRequests.AnyAsync(x => x.Proposal.ConsultationId == proposal.ConsultationId && x.Status != InstallationRequestStatus.RETURNED, ct))
            throw new ConflictException("Installation has already been submitted.");
        var survey = await db.FarmSurveys.Include(x => x.Zones).SingleOrDefaultAsync(x => x.Id == proposal.SurveyId && x.ConsultationId == proposal.ConsultationId, ct)
            ?? throw new BusinessRuleException("Choose a survey from this consultation.");
        if (proposal.Lines.Any(x => !survey.Zones.Any(z => z.Id == x.SurveyZoneId))) throw new BusinessRuleException("Every proposal line must reference a zone in the selected survey.");
        proposal.Revision = latest + 1;
        foreach (var line in proposal.Lines)
        {
            if (line.PublicContentId is not int contentId) throw new BusinessRuleException("Select a published rental product for each line.");
            var price = await db.PublicContent.AsNoTracking().SingleOrDefaultAsync(x => x.Id == contentId && x.Published && x.Kind == PublicContentKind.DEVICE && x.EstimatedMonthlyRentVnd != null, ct)
                ?? throw new BusinessRuleException("Rental product must have a published monthly price.");
            line.PriceRevision = price.Revision; line.MonthlyRentVnd = price.EstimatedMonthlyRentVnd; line.ProductName = price.Title;
        }
        db.DeviceProposals.Add(proposal);
        History(consultation, proposal.CreatedByUserId, ConsultationAction.PROPOSAL_CREATED, $"Proposal revision {proposal.Revision} recorded.", proposal.CreatedAt);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
    }
    public async Task ConfirmAsync(int consultationId, int id, int actorId, bool deposit, string note, DateTimeOffset now, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var consultation = await LockWorkAsync(consultationId, actorId, deposit, ct);
        var proposal = await CurrentAsync(consultationId, id, ct);
        if (proposal.Status != (deposit ? ProposalStatus.PLAN_CONFIRMED : ProposalStatus.DRAFT))
            throw new ConflictException(deposit ? "Confirm the customer plan before recording deposit." : "Plan is already confirmed.");
        if (deposit) { proposal.DepositConfirmedByUserId = actorId; proposal.DepositConfirmedAt = now; proposal.DepositConfirmationNote = note; proposal.Status = ProposalStatus.DEPOSIT_CONFIRMED; }
        else { proposal.PlanConfirmedByUserId = actorId; proposal.PlanConfirmedAt = now; proposal.PlanConfirmationNote = note; proposal.Status = ProposalStatus.PLAN_CONFIRMED; }
        History(consultation, actorId, deposit ? ConsultationAction.DEPOSIT_CONFIRMED : ConsultationAction.PLAN_CONFIRMED, $"Proposal #{id}: {note}", now);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
    }
    public async Task<InstallationRequest> SubmitAsync(int consultationId, int id, int actorId, DateTimeOffset now, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var consultation = await LockWorkAsync(consultationId, actorId, false, ct);
        var proposal = await CurrentAsync(consultationId, id, ct);
        if (proposal.Status != ProposalStatus.DEPOSIT_CONFIRMED) throw new ConflictException("Current plan and external deposit confirmation are required.");
        if (consultation.OwnerId is not int ownerId || consultation.FarmId is not int farmId ||
            !await FarmAccessQuery.AssignedFarmIds(db, ownerId).ContainsAsync(farmId, ct))
            throw new BusinessRuleException("Administrator must link the customer to an authorized farm before submission.");
        if (!await db.Farms.AnyAsync(x => x.Id == farmId && x.IsActive, ct)) throw new BusinessRuleException("Farm must be active.");
        if (proposal.RequestedInstallationAt <= now) throw new BusinessRuleException("Requested installation time has passed. Prepare a revised proposal.");
        if (await db.InstallationRequests.AnyAsync(x => x.Proposal.ConsultationId == consultationId && x.Status != InstallationRequestStatus.RETURNED, ct))
            throw new ConflictException("Installation has already been submitted.");
        var request = new InstallationRequest { ProposalId = id, FarmId = farmId, OwnerId = ownerId, SubmittedByUserId = actorId,
            Status = InstallationRequestStatus.SUBMITTED, SubmittedAt = now };
        db.InstallationRequests.Add(request); proposal.Status = ProposalStatus.SUBMITTED;
        History(consultation, actorId, ConsultationAction.INSTALLATION_SUBMITTED, $"Proposal #{id} submitted for installation review.", now);
        await db.SaveChangesAsync(ct);
        await WorkflowNotifications.AddForRoleAsync(db, RoleNames.FarmAdministrator, "Installation review required", $"Installation request #{request.Id} is awaiting review for farm #{farmId}.", now, ct);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return request;
    }
    private async Task<Consultation> LockWorkAsync(int id, int actorId, bool allowAdmin, CancellationToken ct)
    {
        var user = await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id = {actorId} FOR UPDATE").SingleOrDefaultAsync(ct);
        var roles = await db.UserRoles.Where(x => x.UserId == actorId).Select(x => x.Role.Name).ToArrayAsync(ct);
        if (user is not { IsActive: true } || !(roles.Contains(RoleNames.FarmEngineer) || allowAdmin && roles.Contains(RoleNames.FarmAdministrator)))
            throw new AccessDeniedException("Access denied.");
        var row = await db.Consultations.FromSqlInterpolated($"SELECT * FROM consultations WHERE id = {id} FOR UPDATE").SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException("Consultation not found.");
        if (!(allowAdmin && roles.Contains(RoleNames.FarmAdministrator)) && row.EngineerId != actorId) throw new AccessDeniedException("Assigned engineer access required.");
        if (row.Status != ConsultationStatus.IN_PROGRESS) throw new ConflictException("Consultation is not in progress.");
        return row;
    }
    private async Task<DeviceProposal> CurrentAsync(int consultationId, int id, CancellationToken ct)
    {
        var proposal = await db.DeviceProposals.SingleOrDefaultAsync(x => x.Id == id && x.ConsultationId == consultationId, ct)
            ?? throw new NotFoundException("Proposal not found.");
        if (await db.DeviceProposals.AnyAsync(x => x.ConsultationId == consultationId && x.Revision > proposal.Revision, ct))
            throw new ConflictException("Only the latest proposal can be confirmed or submitted.");
        var latestSurvey = await db.FarmSurveys.Where(x => x.ConsultationId == consultationId).OrderByDescending(x => x.Revision).Select(x => x.Id).FirstAsync(ct);
        if (proposal.SurveyId != latestSurvey) throw new ConflictException("The survey changed. Prepare a new proposal from the latest survey.");
        return proposal;
    }
    private static void History(Consultation row, int userId, ConsultationAction action, string note, DateTimeOffset now)
    {
        row.UpdatedAt = now;
        row.History.Add(new() { UserId = userId, Action = action, Notes = note, Status = row.Status, CreatedAt = now });
    }
}
