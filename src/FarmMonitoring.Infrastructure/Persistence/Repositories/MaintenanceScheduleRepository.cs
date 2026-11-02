using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.TechnicalSupport;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Constants;
using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FarmMonitoring.Infrastructure.Persistence.Repositories;
public sealed partial class MaintenanceScheduleRepository(AppDbContext db) : IMaintenanceScheduleRepository
{
    private Task<bool> HasRole(int id, string role, CancellationToken ct) => db.UserRoles.AnyAsync(x => x.UserId == id && x.User.IsActive && x.Role.Name == role, ct);
    private async Task RequireAdmin(int actorId, CancellationToken ct)
    {
        if (!await HasRole(actorId, RoleNames.FarmAdministrator, ct)) throw new AccessDeniedException("Administrator access required.");
    }
    public async Task<PagedResult<MaintenanceOccurrenceResponse>> OccurrencesAsync(int id, int actorId, PageQuery query, CancellationToken ct)
    {
        await RequireAdmin(actorId, ct);
        if (!await db.MaintenanceSchedules.AnyAsync(x => x.Id == id, ct)) throw new NotFoundException("Maintenance schedule not found.");
        var rows = from occurrence in db.MaintenanceOccurrences.AsNoTracking()
                   join job in db.ServiceJobs on occurrence.JobId equals job.Id
                   join assignment in db.RentalDeviceAssignments on occurrence.RentalDeviceAssignmentId equals assignment.Id
                   where occurrence.ScheduleId == id
                   select new { occurrence, job, assignment };
        var count = await rows.CountAsync(ct);
        var items = await rows.OrderByDescending(x => x.occurrence.DueAt).ThenByDescending(x => x.occurrence.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new MaintenanceOccurrenceResponse(x.occurrence.Id, x.job.Id, x.assignment.Id, x.assignment.Code,
                x.occurrence.DueAt, x.occurrence.CreatedAt, x.occurrence.Policy, x.job.Status.ToString())).ToArrayAsync(ct);
        return new(items, count, query.Page, query.PageSize);
    }
    public async Task<PagedResult<MaintenanceScheduleResponse>> ListAsync(int actorId, PageQuery query, CancellationToken ct)
    {
        await RequireAdmin(actorId, ct);
        var rows = db.MaintenanceSchedules.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search)) rows = rows.Where(x => x.Policy.Contains(query.Search));
        var count = await rows.CountAsync(ct);
        var items = await rows.OrderBy(x => x.NextDueAt).ThenBy(x => x.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToArrayAsync(ct);
        return new(items.Select(MaintenanceScheduleResponse.From).ToArray(), count, query.Page, query.PageSize);
    }
    public async Task<MaintenanceScheduleResponse> GetAsync(int id, int actorId, CancellationToken ct)
    {
        await RequireAdmin(actorId, ct);
        var row = await db.MaintenanceSchedules.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Maintenance schedule not found.");
        return MaintenanceScheduleResponse.From(row);
    }
    private async Task<MaintenanceSchedule> LockAsync(int id, int revision, CancellationToken ct)
    {
        var row = await db.MaintenanceSchedules.FromSqlInterpolated($"SELECT * FROM maintenance_schedules WHERE id = {id} FOR UPDATE").SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException("Maintenance schedule not found.");
        if (row.Revision != revision || row.Revision == int.MaxValue) throw new ConflictException("Schedule changed. Reload its latest revision.");
        return row;
    }
    private async Task<int> CurrentAssignmentIdAsync(int id, CancellationToken ct)
    {
        var seen = new HashSet<int>();
        while (seen.Add(id))
        {
            var next = await db.ServiceDeviceReplacements.Where(x => x.OldAssignmentId == id)
                .Select(x => (int?)x.NewAssignmentId).SingleOrDefaultAsync(ct);
            if (next is null) return id;
            id = next.Value;
        }
        throw new BusinessRuleException("The device replacement history contains a cycle.");
    }
    public async Task<MaintenanceScheduleResponse> SaveAsync(int? id, int actorId, MaintenanceScheduleRequest request, DateTimeOffset now, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await FlightReservation.LockAsync(db, ct);
        await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id IN ({actorId}, {request.TechnicianId}) ORDER BY id FOR UPDATE").ToArrayAsync(ct);
        await RequireAdmin(actorId, ct);
        if (!await HasRole(request.TechnicianId, RoleNames.Technician, ct)) throw new BusinessRuleException("Choose an active Technician.");
        MaintenanceSchedule row;
        if (id is int scheduleId) row = await LockAsync(scheduleId, request.ExpectedRevision, ct);
        else
        {
            if (request.ExpectedRevision != 0) throw new ConflictException("A new schedule has no prior revision.");
            row = new() { CreatedById = actorId, CreatedAt = now };
            db.MaintenanceSchedules.Add(row);
        }
        if (request.RentalDeviceAssignmentId is int assignmentId)
        {
            assignmentId = await CurrentAssignmentIdAsync(assignmentId, ct);
            var assignment = await db.RentalDeviceAssignments.Include(x => x.Contract).SingleOrDefaultAsync(x => x.Id == assignmentId, ct)
                ?? throw new NotFoundException("Rental device assignment not found.");
            if (assignment.ReturnedAt is not null || assignment.Contract.Status == RentalStatus.ENDED)
                throw new BusinessRuleException("Choose equipment that is still assigned.");
            if (!await db.Farms.AnyAsync(x => x.Id == assignment.Contract.FarmId && x.IsActive, ct) ||
                !await FarmAccessQuery.AssignedFarmIds(db, assignment.Contract.OwnerId).ContainsAsync(assignment.Contract.FarmId, ct))
                throw new BusinessRuleException("An active farm and current linked Owner are required.");
        }
        row.RentalDeviceAssignmentId = request.RentalDeviceAssignmentId; row.DeviceType = request.DeviceType;
        row.Policy = request.Policy.Trim(); row.IntervalDays = request.IntervalDays; row.NextDueAt = request.NextDueAt.ToUniversalTime();
        row.TechnicianId = request.TechnicianId; row.Enabled = request.Enabled; row.UpdatedById = actorId; row.UpdatedAt = now;
        if (id.HasValue) row.Revision++;
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return MaintenanceScheduleResponse.From(row);
    }
    public async Task<MaintenanceScheduleResponse> DisableAsync(int id, int actorId, int expectedRevision, DateTimeOffset now, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await FlightReservation.LockAsync(db, ct);
        await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id = {actorId} FOR UPDATE").ToArrayAsync(ct);
        await RequireAdmin(actorId, ct);
        var row = await LockAsync(id, expectedRevision, ct);
        row.Enabled = false; row.Revision++; row.UpdatedById = actorId; row.UpdatedAt = now;
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return MaintenanceScheduleResponse.From(row);
    }
}
