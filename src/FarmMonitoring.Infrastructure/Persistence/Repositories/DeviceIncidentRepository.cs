using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.TechnicalSupport;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Constants;
using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FarmMonitoring.Infrastructure.Persistence.Repositories;
public sealed class DeviceIncidentRepository(AppDbContext db) : IDeviceIncidentRepository
{
    private async Task<IQueryable<DeviceIncident>> ScopeAsync(int actor, CancellationToken ct)
    {
        var roles = await db.UserRoles.Where(x => x.UserId == actor && x.User.IsActive).Select(x => x.Role.Name).ToArrayAsync(ct);
        var admin = roles.Contains(RoleNames.FarmAdministrator);
        if (!admin && !roles.Contains(RoleNames.FarmOwner)) throw new AccessDeniedException("Device incident access denied.");
        var farms = FarmAccessQuery.AssignedFarmIds(db, actor);
        return db.DeviceIncidents.AsNoTracking().Where(x => admin || x.ReporterId == actor && farms.Contains(x.FarmId));
    }
    public async Task<PagedResult<DeviceIncidentSummary>> ListAsync(int actorId, PageQuery query, CancellationToken ct)
    {
        var rows = await ScopeAsync(actorId, ct);
        var count = await rows.CountAsync(ct);
        var items = await rows.OrderByDescending(x => x.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new DeviceIncidentSummary(x.Id, x.FarmId, x.RentalDeviceAssignmentId, x.DeviceCode, x.Description, x.ReportedAt, x.Status.ToString(), x.MissionId)).ToArrayAsync(ct);
        return new(items, count, query.Page, query.PageSize);
    }
    public async Task<DeviceIncidentDetail> GetAsync(int id, int actorId, CancellationToken ct)
    {
        var rows = await ScopeAsync(actorId, ct);
        var row = await rows.Include(x => x.Images).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (row is null)
        {
            if (await db.DeviceIncidents.AnyAsync(x => x.Id == id, ct)) throw new AccessDeniedException("Access to this incident is denied.");
            throw new NotFoundException("Device incident not found.");
        }
        var jobId = await db.ServiceJobs.Where(x => x.IncidentId == id).Select(x => (int?)x.Id).SingleOrDefaultAsync(ct);
        return new(row.Id, row.FarmId, row.RentalDeviceAssignmentId, row.DeviceCode, row.Description, row.ReportedAt, row.Status.ToString(), row.MissionId, row.ReporterId, EvidenceImages.Encode(row.Images), jobId);
    }
    public async Task<int> CreateAsync(int actorId, CreateDeviceIncidentRequest request, List<EvidenceImage> images, DateTimeOffset now, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await FlightReservation.LockAsync(db, ct);
        await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id = {actorId} FOR UPDATE").ToArrayAsync(ct);
        if (!await db.UserRoles.AnyAsync(x => x.UserId == actorId && x.User.IsActive && x.Role.Name == RoleNames.FarmOwner, ct))
            throw new AccessDeniedException("Farm Owner access required.");
        if (!request.RentalDeviceAssignmentId.HasValue)
        {
            if (request.FarmId is not int farmId || !await FarmAccessQuery.AssignedFarmIds(db,actorId).ContainsAsync(farmId,ct)) throw new AccessDeniedException("Choose your farm.");
            if (!await db.Farms.AnyAsync(x => x.Id == farmId && x.IsActive,ct)) throw new BusinessRuleException("Farm must be active.");
            if (request.MissionId.HasValue && !await db.Missions.AnyAsync(x=>x.Id == request.MissionId && x.FarmId == farmId,ct)) throw new BusinessRuleException("Mission must belong to this farm.");
            var unknown = new DeviceIncident { FarmId=farmId,ReporterId=actorId,Description=request.Description.Trim(),ReportedAt=now,MissionId=request.MissionId,Images=images };
            db.DeviceIncidents.Add(unknown); await db.SaveChangesAsync(ct);
            await WorkflowNotifications.AddForRoleAsync(db,RoleNames.FarmAdministrator,"Device data issue reported",$"Incident #{unknown.Id} for farm #{farmId}; device identification required.",now,ct);
            await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return unknown.Id;
        }
        var assignment = await db.RentalDeviceAssignments.Include(x => x.Contract).SingleOrDefaultAsync(x => x.Id == request.RentalDeviceAssignmentId, ct)
            ?? throw new NotFoundException("Rental device assignment not found.");
        var rental = assignment.Contract;
        if (rental.OwnerId != actorId || !await FarmAccessQuery.AssignedFarmIds(db, actorId).ContainsAsync(rental.FarmId, ct))
            throw new AccessDeniedException("Access to this rental device is denied.");
        if (assignment.ReturnedAt is not null || rental.Status == RentalStatus.ENDED)
            throw new BusinessRuleException("Only unreturned assigned devices can be reported.");
        if (request.MissionId is int missionId && !await db.Missions.AnyAsync(x => x.Id == missionId && x.FarmId == rental.FarmId &&
            (assignment.UavId != null && x.UavId == assignment.UavId || assignment.GatewayId != null && x.GatewayId == assignment.GatewayId ||
             assignment.SensorNodeId != null && x.Targets.Any(t => t.SensorNodeId == assignment.SensorNodeId)), ct))
            throw new BusinessRuleException("The linked mission must belong to this farm and include this device.");
        var row = new DeviceIncident { FarmId = rental.FarmId, RentalDeviceAssignmentId = assignment.Id, DeviceCode = assignment.Code,
            ReporterId = actorId, Description = request.Description.Trim(), ReportedAt = now, MissionId = request.MissionId, Images = images };
        db.DeviceIncidents.Add(row);
        await db.SaveChangesAsync(ct);
        await WorkflowNotifications.AddForRoleAsync(db, RoleNames.FarmAdministrator, "Device incident reported",
            $"Device incident #{row.Id} for device {row.DeviceCode}, farm #{row.FarmId}.", now, ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return row.Id;
    }
}
