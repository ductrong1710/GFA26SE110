using FarmMonitoring.Application.Features.TechnicalSupport;
using FarmMonitoring.Domain.Constants;
using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FarmMonitoring.Infrastructure.Persistence.Repositories;
public sealed partial class MaintenanceScheduleRepository
{
    public Task<int[]> DueIdsAsync(int afterId, DateTimeOffset now, CancellationToken ct) => db.MaintenanceSchedules.AsNoTracking()
        .Where(x => x.Enabled && x.NextDueAt <= now && x.Id > afterId).OrderBy(x => x.Id).Take(50).Select(x => x.Id).ToArrayAsync(ct);

    public async Task<int> ProcessDueAsync(int id, DateTimeOffset now, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await FlightReservation.LockAsync(db, ct);
        var schedule = await db.MaintenanceSchedules.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (schedule is null || !schedule.Enabled || schedule.NextDueAt > now || schedule.Revision == int.MaxValue) return 0;
        var candidates = db.RentalDeviceAssignments.Include(x => x.Contract).Where(x => x.ReturnedAt == null && x.Contract.Status != RentalStatus.ENDED);
        if (schedule.RentalDeviceAssignmentId is int original)
        {
            original = await CurrentAssignmentIdAsync(original, ct);
            candidates = candidates.Where(x => x.Id == original);
        }
        else candidates = candidates.Where(x => schedule.DeviceType == "SENSOR" && x.SensorNodeId != null ||
            schedule.DeviceType == "UAV" && x.UavId != null || schedule.DeviceType == "GATEWAY" && x.GatewayId != null);
        var assignments = await candidates.OrderBy(x => x.Id).ToArrayAsync(ct);
        var users = assignments.Select(x => x.Contract.OwnerId).Append(schedule.TechnicianId).Distinct().Order().ToArray();
        await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id = ANY ({users}) ORDER BY id FOR UPDATE").ToArrayAsync(ct);
        schedule = await LockAsync(id, schedule.Revision, ct);
        if (!await HasRole(schedule.TechnicianId, RoleNames.Technician, ct)) return 0;
        var eligible = new List<RentalDeviceAssignment>();
        foreach (var assignment in assignments)
        {
            if (await db.Farms.AnyAsync(x => x.Id == assignment.Contract.FarmId && x.IsActive, ct) &&
                await FarmAccessQuery.AssignedFarmIds(db, assignment.Contract.OwnerId).ContainsAsync(assignment.Contract.FarmId, ct)) eligible.Add(assignment);
        }
        if (eligible.Count == 0) return 0;
        var dueAt = schedule.NextDueAt;
        var created = 0;
        foreach (var assignment in eligible)
        {
            if (await db.MaintenanceOccurrences.AnyAsync(x => x.ScheduleId == id && x.DueAt == dueAt && x.RentalDeviceAssignmentId == assignment.Id, ct)) continue;
            var appointment = now;
            // Preserve the configured technician; place each automatic visit in a free slot.
            while (await TechnicianCalendar.BusyAsync(db, schedule.TechnicianId, appointment, appointment.AddHours(2), null, null, ct))
                appointment = appointment.AddMinutes(30);
            var job = new ServiceJob { RentalDeviceAssignmentId = assignment.Id, Type = ServiceJobType.MAINTENANCE,
                TechnicianId = schedule.TechnicianId, ScheduledAt = appointment, ScheduledEndAt = appointment.AddHours(2), CreatedAt = now };
            job.History.Add(new() { Action = ServiceJobAction.ASSIGNED, UserId = schedule.UpdatedById, TechnicianId = schedule.TechnicianId,
                ScheduledAt = appointment, ScheduledEndAt = appointment.AddHours(2), CreatedAt = now, Note = $"Automatically created from maintenance schedule #{id}: {schedule.Policy}" });
            db.ServiceJobs.Add(job); await db.SaveChangesAsync(ct);
            db.MaintenanceOccurrences.Add(new() { ScheduleId = id, DueAt = dueAt, RentalDeviceAssignmentId = assignment.Id,
                JobId = job.Id, Policy = schedule.Policy, CreatedAt = now });
            var message = $"Maintenance job #{job.Id} is due for device {assignment.Code}. {schedule.Policy}";
            foreach (var recipientId in new[] { schedule.TechnicianId, assignment.Contract.OwnerId }.Distinct())
                await WorkflowNotifications.AddForUserAsync(db, recipientId, "Maintenance due", message, now, ct);
            created++;
        }
        schedule.NextDueAt = MaintenanceRecurrence.After(dueAt, schedule.IntervalDays, now);
        schedule.Revision++; schedule.UpdatedAt = now;
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return created;
    }
}
