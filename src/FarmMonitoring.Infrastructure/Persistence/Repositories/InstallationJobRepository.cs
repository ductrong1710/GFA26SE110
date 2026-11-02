using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Consultations;
using FarmMonitoring.Application.Features.Installations;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Constants;
using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FarmMonitoring.Infrastructure.Persistence.Repositories;

public sealed class InstallationJobRepository(AppDbContext db) : IInstallationJobRepository
{
    private async Task<string[]> RolesAsync(int actorId, CancellationToken ct) => await db.UserRoles
        .Where(x => x.UserId == actorId && x.User.IsActive).Select(x => x.Role.Name).ToArrayAsync(ct);
    private async Task<IQueryable<InstallationJob>> ScopedAsync(int actorId, CancellationToken ct)
    {
        var roles = await RolesAsync(actorId, ct);
        var admin = roles.Contains(RoleNames.FarmAdministrator);
        var tech = roles.Contains(RoleNames.Technician);
        var engineer = roles.Contains(RoleNames.FarmEngineer);
        var farms = FarmAccessQuery.AssignedFarmIds(db, actorId);
        return db.InstallationJobs.AsNoTracking().Where(x => admin || tech && x.TechnicianId == actorId ||
            engineer && x.InstallationRequest.Proposal.Consultation.EngineerId == actorId ||
            x.InstallationRequest.OwnerId == actorId && farms.Contains(x.InstallationRequest.FarmId));
    }
    public async Task<PagedResult<JobSummary>> ListAsync(int actorId, PageQuery query, CancellationToken ct)
    {
        var rows = await ScopedAsync(actorId, ct);
        var count = await rows.CountAsync(ct);
        var items = await rows.OrderBy(x => x.ScheduledAt).ThenBy(x => x.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new JobSummary(x.Id, x.InstallationRequestId, x.InstallationRequest.FarmId, x.TechnicianId, x.ScheduledAt, x.Status.ToString(), x.Revision)).ToArrayAsync(ct);
        return new(items, count, query.Page, query.PageSize);
    }
    public async Task<PagedResult<TechnicianOption>> TechniciansAsync(int actorId, PageQuery query, CancellationToken ct)
    {
        if (!(await RolesAsync(actorId, ct)).Contains(RoleNames.FarmAdministrator)) throw new AccessDeniedException("Administrator access required.");
        var rows = db.Users.AsNoTracking().Where(x => x.IsActive && x.UserRoles.Any(r => r.Role.Name == RoleNames.Technician));
        var count = await rows.CountAsync(ct);
        return new(await rows.OrderBy(x => x.FullName).ThenBy(x => x.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new TechnicianOption(x.Id, x.FullName)).ToArrayAsync(ct), count, query.Page, query.PageSize);
    }
    public async Task<JobDetail> GetAsync(int id, int actorId, CancellationToken ct)
    {
        var rows = await ScopedAsync(actorId, ct);
        var row = await rows.Include(x => x.History).Include(x => x.InstallationRequest).ThenInclude(x => x.Proposal).ThenInclude(x => x.Lines)
            .Include(x => x.InstallationRequest).ThenInclude(x => x.Proposal).ThenInclude(x => x.Survey).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (row is null)
        {
            if (await db.InstallationJobs.AnyAsync(x => x.Id == id, ct)) throw new AccessDeniedException("Access to this job is denied.");
            throw new NotFoundException("Installation job not found.");
        }
        var proposal = row.InstallationRequest.Proposal;
        return new(row.Id, row.InstallationRequestId, row.InstallationRequest.FarmId, row.TechnicianId, row.ScheduledAt, row.Status.ToString(), row.Revision,
            proposal.Survey.FarmName, proposal.Survey.FarmLocation,
            proposal.Lines.OrderBy(x => x.Id).Select(x => new ProposalLineResponse(x.Id, x.SurveyZoneId, x.DeviceType, x.Quantity, x.InstallationLocation)).ToArray(),
            row.History.OrderBy(x => x.Id).Select(x => new JobHistoryResponse(x.Id, x.Action.ToString(), x.UserId, x.TechnicianId, x.ScheduledAt, x.Note, x.CreatedAt)).ToArray());
    }
    public async Task<int> AssignAsync(int id, bool create, int actorId, JobAssignmentRequest request, DateTimeOffset now, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // Stable user ordering serializes eligibility changes without opposing user locks.
        await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id IN ({actorId}, {request.TechnicianId}) ORDER BY id FOR UPDATE").ToArrayAsync(ct);
        if (!(await RolesAsync(actorId, ct)).Contains(RoleNames.FarmAdministrator)) throw new AccessDeniedException("Administrator access required.");
        if (!(await RolesAsync(request.TechnicianId, ct)).Contains(RoleNames.Technician)) throw new BusinessRuleException("Choose an active Technician.");
        InstallationJob job;
        if (create)
        {
            var installation = await db.InstallationRequests.FromSqlInterpolated($"SELECT * FROM installation_requests WHERE id = {id} FOR UPDATE").SingleOrDefaultAsync(ct)
                ?? throw new NotFoundException("Installation request not found.");
            if (installation.Status != InstallationRequestStatus.APPROVED) throw new ConflictException("Approve the installation request before scheduling work.");
            if (await db.InstallationJobs.AnyAsync(x => x.InstallationRequestId == id, ct)) throw new ConflictException("This request already has an installation job.");
            job = new() { InstallationRequestId = id, CreatedAt = now };
            db.InstallationJobs.Add(job);
        }
        else
        {
            job = await LockAsync(id, ct);
            if (job.Revision != request.ExpectedRevision) throw new ConflictException("Job changed. Reload before assigning.");
            if (job.Status is not (InstallationJobStatus.ASSIGNED or InstallationJobStatus.IN_PROGRESS or InstallationJobStatus.REWORK)) throw new ConflictException("This job cannot be reassigned.");
            job.Revision++;
        }
        var previousTechnician = job.TechnicianId;
        job.TechnicianId = request.TechnicianId; job.ScheduledAt = request.ScheduledAt.ToUniversalTime(); job.Status = InstallationJobStatus.ASSIGNED;
        History(job, create ? InstallationJobAction.ASSIGNED : InstallationJobAction.REASSIGNED, actorId, request.Note.Trim(), now);
        await db.SaveChangesAsync(ct);
        await WorkflowNotifications.AddForUserAsync(db, job.TechnicianId, "Installation assignment", $"Installation job #{job.Id} is assigned to you. Open your jobs to review the appointment.", now, ct);
        if (previousTechnician != 0 && previousTechnician != job.TechnicianId)
            await WorkflowNotifications.AddForUserAsync(db, previousTechnician, "Installation assignment changed", $"Installation job #{job.Id} is no longer assigned to you.", now, ct);
        var ownerId = await db.InstallationRequests.Where(x => x.Id == job.InstallationRequestId).Select(x => x.OwnerId).SingleAsync(ct);
        await WorkflowNotifications.AddForUserAsync(db, ownerId, "Installation appointment updated", $"Installation job #{job.Id} has an updated appointment. Open installation jobs to review it.", now, ct);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return job.Id;
    }
    public async Task StartAsync(int id, int actorId, int expectedRevision, DateTimeOffset now, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id = {actorId} FOR UPDATE").ToArrayAsync(ct);
        if (!(await RolesAsync(actorId, ct)).Contains(RoleNames.Technician)) throw new AccessDeniedException("Technician access required.");
        var job = await LockAsync(id, ct);
        if (job.TechnicianId != actorId) throw new AccessDeniedException("Only the assigned Technician can start this job.");
        if (job.Revision != expectedRevision || job.Status != InstallationJobStatus.ASSIGNED) throw new ConflictException("Job changed or already started.");
        job.Status = InstallationJobStatus.IN_PROGRESS; job.Revision++;
        History(job, InstallationJobAction.STARTED, actorId, "Installation work started.", now);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
    }
    private async Task<InstallationJob> LockAsync(int id, CancellationToken ct) =>
        await db.InstallationJobs.FromSqlInterpolated($"SELECT * FROM installation_jobs WHERE id = {id} FOR UPDATE").SingleOrDefaultAsync(ct)
        ?? throw new NotFoundException("Installation job not found.");
    private static void History(InstallationJob job, InstallationJobAction action, int actorId, string note, DateTimeOffset now) =>
        job.History.Add(new() { Action = action, UserId = actorId, TechnicianId = job.TechnicianId, ScheduledAt = job.ScheduledAt, Note = note, CreatedAt = now });
}
