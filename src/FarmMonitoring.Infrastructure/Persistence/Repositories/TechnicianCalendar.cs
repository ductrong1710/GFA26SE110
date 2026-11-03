using FarmMonitoring.Application.Common;
using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FarmMonitoring.Infrastructure.Persistence.Repositories;

internal static class TechnicianCalendar
{
    // Assignment writers lock the technician row before testing both job tables.
    public static async Task<bool> BusyAsync(AppDbContext db, int technicianId, DateTimeOffset start, DateTimeOffset end,
        int? installationId, int? serviceId, CancellationToken ct)
    {
        var lower = start - OperationalRules.TravelBuffer;
        var upper = end + OperationalRules.TravelBuffer;
        return await db.InstallationJobs.AnyAsync(x => x.TechnicianId == technicianId && x.Id != installationId &&
            x.Status != InstallationJobStatus.APPROVED && x.ScheduledAt < upper &&
            (x.ScheduledEndAt > x.ScheduledAt ? x.ScheduledEndAt : x.ScheduledAt.AddHours(2)) > lower, ct) ||
            await db.ServiceJobs.AnyAsync(x => x.TechnicianId == technicianId && x.Id != serviceId &&
            x.Status != ServiceJobStatus.CLOSED && x.ScheduledAt < upper &&
            (x.ScheduledEndAt > x.ScheduledAt ? x.ScheduledEndAt : x.ScheduledAt.AddHours(2)) > lower, ct);
    }
    public static async Task EnsureFreeAsync(AppDbContext db, int technicianId, DateTimeOffset start, DateTimeOffset end,
        int? installationId, int? serviceId, CancellationToken ct)
    {
        if (await BusyAsync(db, technicianId, start, end, installationId, serviceId, ct))
            throw new ConflictException("Technician has an overlapping installation/service appointment (including 30-minute travel buffer).");
    }
}
