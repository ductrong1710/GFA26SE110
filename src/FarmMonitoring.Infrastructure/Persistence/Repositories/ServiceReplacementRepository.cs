using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.TechnicalSupport;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Constants;
using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FarmMonitoring.Infrastructure.Persistence.Repositories;
public sealed class ServiceReplacementRepository(AppDbContext db) : IServiceReplacementRepository
{
    private Task<string[]> RolesAsync(int actor, CancellationToken ct) => db.UserRoles.Where(x => x.UserId == actor && x.User.IsActive).Select(x => x.Role.Name).ToArrayAsync(ct);
    private async Task<RentalDeviceAssignment> CurrentAsync(ServiceJob job, CancellationToken ct)
    {
        var replacement = await db.ServiceDeviceReplacements.Where(x => x.JobId == job.Id).OrderByDescending(x => x.Id).Select(x => (int?)x.NewAssignmentId).FirstOrDefaultAsync(ct);
        var id = replacement ?? job.RentalDeviceAssignmentId;
        return await db.RentalDeviceAssignments.Include(x => x.Contract).SingleAsync(x => x.Id == id, ct);
    }
    private IQueryable<ReplacementOption> Candidates(RentalDeviceAssignment old)
    {
        var allocations = db.InstallationDeviceAllocations.Where(x => x.ReleasedAt == null);
        var rentals = db.RentalDeviceAssignments.Where(x => x.ReturnedAt == null);
        if (old.SensorNodeId.HasValue)
            return db.SensorNodes.Where(x => x.IsActive && x.Zone.FarmId == old.Contract.FarmId && !allocations.Any(a => a.SensorNodeId == x.Id) && !rentals.Any(a => a.SensorNodeId == x.Id))
                .Select(x => new ReplacementOption { SensorNodeId = x.Id, UavId = null, GatewayId = null, Code = x.DeviceCode, Name = x.Name });
        if (old.UavId.HasValue)
            return db.Uavs.Where(x => x.IsActive && !allocations.Any(a => a.UavId == x.Id) && !rentals.Any(a => a.UavId == x.Id))
                .Select(x => new ReplacementOption { SensorNodeId = null, UavId = x.Id, GatewayId = null, Code = x.Code, Name = x.Name });
        return db.Gateways.Where(x => x.IsActive && !allocations.Any(a => a.GatewayId == x.Id) && !rentals.Any(a => a.GatewayId == x.Id))
            .Select(x => new ReplacementOption { SensorNodeId = null, UavId = null, GatewayId = x.Id, Code = x.Code, Name = x.Name });
    }
    public async Task<PagedResult<ReplacementOption>> OptionsAsync(int jobId, int actorId, PageQuery query, CancellationToken ct)
    {
        var roles = await RolesAsync(actorId, ct);
        if (!roles.Contains(RoleNames.FarmAdministrator) && !roles.Contains(RoleNames.Technician)) throw new AccessDeniedException("Technical job access required.");
        var job = await db.ServiceJobs.AsNoTracking().SingleOrDefaultAsync(x => x.Id == jobId, ct) ?? throw new NotFoundException("Service job not found.");
        if (!roles.Contains(RoleNames.FarmAdministrator) && job.TechnicianId != actorId) throw new AccessDeniedException("Assigned Technician access required.");
        var old = await CurrentAsync(job, ct);
        var rows = Candidates(old);
        if (!string.IsNullOrWhiteSpace(query.Search)) rows = rows.Where(x => x.Code.Contains(query.Search) || x.Name.Contains(query.Search));
        var count = await rows.CountAsync(ct);
        return new(await rows.OrderBy(x => x.Code).ThenBy(x => x.SensorNodeId).ThenBy(x => x.UavId).ThenBy(x => x.GatewayId).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToArrayAsync(ct), count, query.Page, query.PageSize);
    }
    public async Task ReplaceAsync(int jobId, int actorId, ServiceReplacementRequest request, DateTimeOffset now, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await FlightReservation.LockAsync(db, ct);
        await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id = {actorId} FOR UPDATE").ToArrayAsync(ct);
        if (!(await RolesAsync(actorId, ct)).Contains(RoleNames.Technician)) throw new AccessDeniedException("Technician access required.");
        var job = await db.ServiceJobs.FromSqlInterpolated($"SELECT * FROM service_jobs WHERE id = {jobId} FOR UPDATE").SingleOrDefaultAsync(ct) ?? throw new NotFoundException("Service job not found.");
        if (job.TechnicianId != actorId) throw new AccessDeniedException("Assigned Technician access required.");
        if (job.Revision != request.ExpectedRevision || job.Status is not (ServiceJobStatus.IN_PROGRESS or ServiceJobStatus.REWORK)) throw new ConflictException("Start work or reload the current job before replacing equipment.");
        var old = await CurrentAsync(job, ct);
        await db.RentalContracts.FromSqlInterpolated($"SELECT * FROM rental_contracts WHERE id = {old.ContractId} FOR UPDATE").ToArrayAsync(ct);
        if (old.ReturnedAt is not null || old.Contract.Status == RentalStatus.ENDED) throw new ConflictException("The affected equipment is no longer assigned.");
        if (!await db.Farms.AnyAsync(x => x.Id == old.Contract.FarmId && x.IsActive, ct) || !await FarmAccessQuery.AssignedFarmIds(db, old.Contract.OwnerId).ContainsAsync(old.Contract.FarmId, ct))
            throw new BusinessRuleException("An active farm and current linked Owner are required.");
        if (old.SensorNodeId.HasValue != request.SensorNodeId.HasValue || old.UavId.HasValue != request.UavId.HasValue || old.GatewayId.HasValue != request.GatewayId.HasValue)
            throw new BusinessRuleException("Use the same physical device type.");
        var target = await Candidates(old).SingleOrDefaultAsync(x => x.SensorNodeId == request.SensorNodeId && x.UavId == request.UavId && x.GatewayId == request.GatewayId, ct)
            ?? throw new ConflictException("Replacement device is unavailable or already reserved.");
        var uavs = new[] { old.UavId, request.UavId }.Where(x => x.HasValue).Select(x => x!.Value).ToArray();
        var gateways = new[] { old.GatewayId, request.GatewayId }.Where(x => x.HasValue).Select(x => x!.Value).ToArray();
        var sensors = new[] { old.SensorNodeId, request.SensorNodeId }.Where(x => x.HasValue).Select(x => x!.Value).ToArray();
        if (await db.Missions.AnyAsync(x => x.Status == MissionStatus.RUNNING &&
            (x.UavId.HasValue && uavs.Contains(x.UavId.Value) || x.GatewayId.HasValue && gateways.Contains(x.GatewayId.Value) || x.Targets.Any(t => sensors.Contains(t.SensorNodeId))), ct) ||
            await FlightReservation.Active(db, now).AnyAsync(x => uavs.Contains(x.UavId) || gateways.Contains(x.GatewayId), ct))
            throw new ConflictException("Finish active or recovery flight operations before replacing equipment.");
        var oldAllocation = await db.InstallationDeviceAllocations.SingleAsync(x => x.Id == old.InstallationAllocationId, ct);
        if (oldAllocation.ReleasedAt is not null) throw new ConflictException("The old device reservation has already been released.");
        old.ReturnedAt = now; oldAllocation.ReleasedAt = now;
        // Retrieved failed equipment requires explicit administrator reactivation after repair.
        if (old.SensorNodeId is int sensor) await db.SensorNodes.Where(x => x.Id == sensor).ExecuteUpdateAsync(x => x.SetProperty(d => d.IsActive, false), ct);
        if (old.UavId is int uav) await db.Uavs.Where(x => x.Id == uav).ExecuteUpdateAsync(x => x.SetProperty(d => d.IsActive, false), ct);
        if (old.GatewayId is int gateway) await db.Gateways.Where(x => x.Id == gateway).ExecuteUpdateAsync(x => x.SetProperty(d => d.IsActive, false), ct);
        await db.SaveChangesAsync(ct);
        var allocation = new InstallationDeviceAllocation { JobId = oldAllocation.JobId, ProposalLineId = oldAllocation.ProposalLineId,
            SensorNodeId = target.SensorNodeId, UavId = target.UavId, GatewayId = target.GatewayId, Code = target.Code, Name = target.Name, AllocatedByUserId = actorId, AllocatedAt = now };
        db.InstallationDeviceAllocations.Add(allocation);
        try
        {
            await db.SaveChangesAsync(ct);
            var next = new RentalDeviceAssignment { ContractId = old.ContractId, InstallationAllocationId = allocation.Id,
                SensorNodeId = target.SensorNodeId, UavId = target.UavId, GatewayId = target.GatewayId, Code = target.Code, Name = target.Name, InstallationLocation = old.InstallationLocation };
            db.RentalDeviceAssignments.Add(next);
            var replacement = new ServiceDeviceReplacement { JobId = jobId, OldAssignment = old, NewAssignment = next, OldDeviceCode = old.Code, NewDeviceCode = next.Code, TechnicianId = actorId, CreatedAt = now, Note = request.Note.Trim() };
            db.ServiceDeviceReplacements.Add(replacement);
            job.Revision++; old.Contract.Revision++;
            job.History.Add(new() { Action = ServiceJobAction.DEVICE_REPLACED, UserId = actorId, TechnicianId = actorId, ScheduledAt = job.ScheduledAt, Note = $"{old.Code} -> {next.Code}: {request.Note.Trim()}", CreatedAt = now });
            old.Contract.History.Add(new() { Action = "DEVICE_REPLACED", UserId = actorId, Note = $"Service job #{jobId}: {old.Code} -> {next.Code}", CreatedAt = now, StartAt = old.Contract.StartAt, EndAt = old.Contract.EndAt });
            await WorkflowNotifications.AddForUserAsync(db, old.Contract.OwnerId, "Service device replaced", $"Device {old.Code} replaced by {next.Code} for service job #{jobId}; review the subsequent service report.", now, ct);
            await WorkflowNotifications.AddForRoleAsync(db, RoleNames.FarmAdministrator, "Service device replaced", $"Service job #{jobId}: {old.Code} replaced by {next.Code}.", now, ct);
            await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { throw new ConflictException("Replacement device was reserved concurrently. Reload available devices."); }
    }
}
