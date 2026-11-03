using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.TechnicalSupport;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Constants;
using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FarmMonitoring.Infrastructure.Persistence.Repositories;
public sealed class ServiceJobRepository(AppDbContext db) : IServiceJobRepository
{
    private Task<string[]> RolesAsync(int actor, CancellationToken ct) => db.UserRoles.Where(x => x.UserId == actor && x.User.IsActive).Select(x => x.Role.Name).ToArrayAsync(ct);
    private async Task<IQueryable<ServiceJob>> ScopeAsync(int actor, CancellationToken ct)
    {
        var roles = await RolesAsync(actor, ct);
        var admin = roles.Contains(RoleNames.FarmAdministrator);
        var technician = roles.Contains(RoleNames.Technician);
        var owner = roles.Contains(RoleNames.FarmOwner);
        if (!admin && !technician && !owner) throw new AccessDeniedException("Service job access denied.");
        var farms = FarmAccessQuery.AssignedFarmIds(db, actor);
        return db.ServiceJobs.AsNoTracking().Where(x => admin || technician && x.TechnicianId == actor ||
            owner && x.RentalDeviceAssignment.Contract.OwnerId == actor && farms.Contains(x.RentalDeviceAssignment.Contract.FarmId));
    }
    public async Task<PagedResult<ServiceJobSummary>> ListAsync(int actorId, PageQuery query, CancellationToken ct)
    {
        var rows = await ScopeAsync(actorId, ct);
        var count = await rows.CountAsync(ct);
        var items = await rows.OrderBy(x => x.ScheduledAt).ThenBy(x => x.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new ServiceJobSummary(x.Id, x.IncidentId, x.RentalDeviceAssignment.Contract.FarmId, x.RentalDeviceAssignment.Code, x.Type.ToString(), x.TechnicianId, x.ScheduledAt, x.Status.ToString(), x.Revision)).ToArrayAsync(ct);
        return new(items, count, query.Page, query.PageSize);
    }
    public async Task<ServiceJobDetail> GetAsync(int id, int actorId, CancellationToken ct)
    {
        var rows = await ScopeAsync(actorId, ct);
        var row = await rows.Include(x => x.Replacements).Include(x => x.History).Include(x => x.Incident).ThenInclude(x => x!.Images)
            .Include(x => x.RentalDeviceAssignment).ThenInclude(x => x.Contract).AsSplitQuery().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (row is null)
        {
            if (await db.ServiceJobs.AnyAsync(x => x.Id == id, ct)) throw new AccessDeniedException("Access to this service job is denied.");
            throw new NotFoundException("Service job not found.");
        }
        return new(row.Id, row.IncidentId, row.RentalDeviceAssignment.Contract.FarmId, row.RentalDeviceAssignment.Code, row.Type.ToString(), row.TechnicianId, row.ScheduledAt, row.Status.ToString(), row.Revision,
            row.Incident?.Description ?? "Preventive maintenance", row.RentalDeviceAssignment.InstallationLocation, EvidenceImages.Encode(row.Incident?.Images ?? []),
            row.History.OrderBy(x => x.Id).Select(x => new ServiceHistoryResponse(x.Id, x.Action.ToString(), x.UserId, x.TechnicianId, x.ScheduledAt, x.Note, x.CreatedAt)).ToArray(),
            row.Replacements.OrderByDescending(x => x.Id).FirstOrDefault()?.NewDeviceCode ?? row.RentalDeviceAssignment.Code,
            row.Replacements.OrderBy(x => x.Id).Select(ServiceReplacementResponse.From).ToArray());
    }
    public async Task<int> AssignAsync(int id, bool create, int actorId, ServiceJobAssignmentRequest request, DateTimeOffset now, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id IN ({actorId}, {request.TechnicianId}) ORDER BY id FOR UPDATE").ToArrayAsync(ct);
        if (!(await RolesAsync(actorId, ct)).Contains(RoleNames.FarmAdministrator)) throw new AccessDeniedException("Administrator access required.");
        if (!(await RolesAsync(request.TechnicianId, ct)).Contains(RoleNames.Technician)) throw new BusinessRuleException("Choose an active Technician.");
        ServiceJob job;
        if (create)
        {
            var incident = await db.DeviceIncidents.FromSqlInterpolated($"SELECT * FROM device_incidents WHERE id = {id} FOR UPDATE").SingleOrDefaultAsync(ct)
                ?? throw new NotFoundException("Device incident not found.");
            if (request.ExpectedRevision != 0) throw new ConflictException("A new service job has no prior revision.");
            if (await db.ServiceJobs.AnyAsync(x => x.IncidentId == id, ct)) throw new ConflictException("This incident already has a service job.");
            var assignmentId = incident.RentalDeviceAssignmentId ?? request.RentalDeviceAssignmentId ?? throw new BusinessRuleException("Identify the affected farm device before creating a service job.");
            if (!await db.RentalDeviceAssignments.AnyAsync(x => x.Id == assignmentId && x.Contract.FarmId == incident.FarmId && x.ReturnedAt == null && x.Contract.Status != RentalStatus.ENDED,ct))
                throw new BusinessRuleException("Choose current equipment in the reported farm.");
            job = new() { Incident = incident, IncidentId = id, RentalDeviceAssignmentId = assignmentId, Type = Enum.Parse<ServiceJobType>(request.Type), CreatedAt = now };
            db.ServiceJobs.Add(job);
        }
        else
        {
            job = await LockAsync(id, ct);
            if (job.Revision != request.ExpectedRevision) throw new ConflictException("Service job changed. Reload the latest revision.");
            if (job.Status is not (ServiceJobStatus.ASSIGNED or ServiceJobStatus.IN_PROGRESS or ServiceJobStatus.REWORK)) throw new ConflictException("This job cannot be reassigned.");
            if (job.Type.ToString() != request.Type) throw new ConflictException("Keep the recorded service type when rescheduling.");
            job.Revision++;
        }
        var end = (request.ScheduledEndAt ?? request.ScheduledAt.AddHours(2)).ToUniversalTime();
        await TechnicianCalendar.EnsureFreeAsync(db, request.TechnicianId, request.ScheduledAt.ToUniversalTime(), end, null, create ? null : job.Id, ct);
        var previousTechnician = job.TechnicianId;
        job.ScheduledEndAt = end;
        job.TechnicianId = request.TechnicianId; job.ScheduledAt = request.ScheduledAt.ToUniversalTime(); job.Status = ServiceJobStatus.ASSIGNED;
        History(job, create ? ServiceJobAction.ASSIGNED : ServiceJobAction.REASSIGNED, actorId, request.Note, now);
        await db.SaveChangesAsync(ct);
        await WorkflowNotifications.AddForUserAsync(db, job.TechnicianId, "Service job assigned", $"Service job #{job.Id} is assigned for {job.ScheduledAt:u}.", now, ct);
        if (previousTechnician != 0 && previousTechnician != job.TechnicianId)
            await WorkflowNotifications.AddForUserAsync(db, previousTechnician, "Service assignment changed", $"Service job #{job.Id} is no longer assigned to you.", now, ct);
        var assignment = await db.RentalDeviceAssignments.Include(x => x.Contract).SingleAsync(x => x.Id == job.RentalDeviceAssignmentId, ct);
        var ownerId = assignment.Contract.OwnerId;
        if (await FarmAccessQuery.AssignedFarmIds(db, ownerId).ContainsAsync(assignment.Contract.FarmId, ct))
            await WorkflowNotifications.AddForUserAsync(db, ownerId, "Device service scheduled", $"Service job #{job.Id} is scheduled for {job.ScheduledAt:u}.", now, ct);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return job.Id;
    }
    public async Task StartAsync(int id, int actorId, int expectedRevision, DateTimeOffset now, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id = {actorId} FOR UPDATE").ToArrayAsync(ct);
        if (!(await RolesAsync(actorId, ct)).Contains(RoleNames.Technician)) throw new AccessDeniedException("Technician access required.");
        var job = await LockAsync(id, ct);
        if (job.TechnicianId != actorId) throw new AccessDeniedException("Only the assigned Technician can start this job.");
        if (job.Revision != expectedRevision || job.Status != ServiceJobStatus.ASSIGNED) throw new ConflictException("Service job changed or already started.");
        job.Status = ServiceJobStatus.IN_PROGRESS; job.Revision++;
        History(job, ServiceJobAction.STARTED, actorId, "Service work started.", now);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
    }
    private async Task<ServiceJob> LockAsync(int id, CancellationToken ct) =>
        await db.ServiceJobs.FromSqlInterpolated($"SELECT * FROM service_jobs WHERE id = {id} FOR UPDATE").SingleOrDefaultAsync(ct)
        ?? throw new NotFoundException("Service job not found.");
    private static void History(ServiceJob job, ServiceJobAction action, int actor, string note, DateTimeOffset now) =>
        job.History.Add(new() { Action = action, UserId = actor, TechnicianId = job.TechnicianId, ScheduledAt = job.ScheduledAt, ScheduledEndAt = job.ScheduledEndAt, Note = note.Trim(), CreatedAt = now });
}
