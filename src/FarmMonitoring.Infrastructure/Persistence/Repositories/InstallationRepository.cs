using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Installations;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Constants;
using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FarmMonitoring.Infrastructure.Persistence.Repositories;

public sealed class InstallationRepository(AppDbContext db) : IInstallationRepository
{
    public async Task<PagedResult<InstallationSummary>> ListAsync(int actorId, bool admin, bool engineer, PageQuery query, CancellationToken ct)
    {
        var farms = FarmAccessQuery.AssignedFarmIds(db, actorId);
        var rows = db.InstallationRequests.AsNoTracking().Where(x => admin || engineer && x.Proposal.Consultation.EngineerId == actorId || x.OwnerId == actorId && farms.Contains(x.FarmId));
        var total = await rows.CountAsync(ct);
        var items = await rows.OrderByDescending(x => x.SubmittedAt).ThenByDescending(x => x.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new InstallationSummary(x.Id, x.Proposal.ConsultationId, x.ProposalId, x.FarmId, x.OwnerId, x.Status.ToString(), x.SubmittedAt)).ToArrayAsync(ct);
        return new(items, total, query.Page, query.PageSize);
    }
    public Task<InstallationRequest?> GetAsync(int id, CancellationToken ct) => db.InstallationRequests.AsNoTracking()
        .Include(x => x.Proposal).ThenInclude(x => x.Lines).SingleOrDefaultAsync(x => x.Id == id, ct);

    public async Task ReviewAsync(int id, int actorId, bool approve, string note, DateTimeOffset now, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var actor = await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id = {actorId} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (actor is not { IsActive: true } || !await db.UserRoles.AnyAsync(x => x.UserId == actorId && x.Role.Name == RoleNames.FarmAdministrator, ct))
            throw new AccessDeniedException("Administrator access required.");
        var consultationId = await db.InstallationRequests.Where(x => x.Id == id).Select(x => (int?)x.Proposal.ConsultationId).SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException("Installation request not found.");
        // Match proposal mutations: lock the consultation before the request to serialize return/resubmission.
        var consultation = await db.Consultations.FromSqlInterpolated($"SELECT * FROM consultations WHERE id = {consultationId} FOR UPDATE").SingleAsync(ct);
        var row = await db.InstallationRequests.FromSqlInterpolated($"SELECT * FROM installation_requests WHERE id = {id} FOR UPDATE").SingleAsync(ct);
        if (row.Status != InstallationRequestStatus.SUBMITTED) throw new ConflictException("Only submitted installation requests can be reviewed.");
        if (approve)
        {
            var proposal = await db.DeviceProposals.Include(x => x.Lines).SingleAsync(x => x.Id == row.ProposalId, ct);
            if (proposal.Status != ProposalStatus.SUBMITTED || proposal.PlanConfirmedAt is null || proposal.DepositConfirmedAt is null || proposal.Lines.Count == 0)
                throw new BusinessRuleException("Confirmed plan, deposit and device information are required.");
            if (proposal.RequestedInstallationAt <= now) throw new BusinessRuleException("Requested schedule has passed. Return the request for correction.");
            if (!await db.Farms.AnyAsync(x => x.Id == row.FarmId && x.IsActive, ct) || !await FarmAccessQuery.AssignedFarmIds(db, row.OwnerId).ContainsAsync(row.FarmId, ct))
                throw new BusinessRuleException("An active farm and currently authorized customer are required.");
        }
        row.Status = approve ? InstallationRequestStatus.APPROVED : InstallationRequestStatus.RETURNED;
        row.ReviewedByUserId = actorId; row.ReviewedAt = now; row.ReviewNote = note;
        consultation.UpdatedAt = now;
        consultation.History.Add(new() { UserId = actorId, Action = approve ? ConsultationAction.INSTALLATION_APPROVED : ConsultationAction.INSTALLATION_RETURNED,
            Status = consultation.Status, Notes = $"Installation request #{id}: {note}", CreatedAt = now });
        await WorkflowNotifications.AddForUserAsync(db, row.SubmittedByUserId, approve ? "Installation request approved" : "Installation correction required",
            $"Installation request #{id} was {(approve ? "approved for scheduling" : "returned for correction")}. Open installation requests to view the decision.", now, ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
