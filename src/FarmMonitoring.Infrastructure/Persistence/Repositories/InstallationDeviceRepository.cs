using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Installations;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Constants;
using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;
namespace FarmMonitoring.Infrastructure.Persistence.Repositories;
public sealed class InstallationDeviceRepository(AppDbContext db) : IInstallationDeviceRepository
{
    public async Task<IReadOnlyList<AllocatedDeviceResponse>> GetAsync(int jobId, CancellationToken ct) => await db.InstallationDeviceAllocations.AsNoTracking()
        .Where(x => x.JobId == jobId && x.ReleasedAt == null).OrderBy(x => x.Id)
        .Select(x => new AllocatedDeviceResponse(x.Id, x.ProposalLineId, x.SensorNodeId, x.UavId, x.GatewayId, x.Code, x.Name)).ToArrayAsync(ct);
    public async Task SetAsync(int jobId, int actorId, JobDevicesRequest request, DateTimeOffset now, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id = {actorId} FOR UPDATE").ToArrayAsync(ct);
        if (!await db.UserRoles.AnyAsync(x => x.UserId == actorId && x.User.IsActive && x.Role.Name == RoleNames.FarmAdministrator, ct)) throw new AccessDeniedException("Administrator access required.");
        var job = await db.InstallationJobs.FromSqlInterpolated($"SELECT * FROM installation_jobs WHERE id = {jobId} FOR UPDATE").SingleOrDefaultAsync(ct) ?? throw new NotFoundException("Installation job not found.");
        if (job.Revision != request.ExpectedRevision || job.Status is not (InstallationJobStatus.ASSIGNED or InstallationJobStatus.IN_PROGRESS or InstallationJobStatus.REWORK)) throw new ConflictException("Job changed or allocation is already submitted for review.");
        var installation = await db.InstallationRequests.Include(x => x.Proposal).ThenInclude(x => x.Lines).SingleAsync(x => x.Id == job.InstallationRequestId, ct);
        if (request.Devices.Any(d => installation.Proposal.Lines.All(l => l.Id != d.ProposalLineId)) || installation.Proposal.Lines.Any(l => request.Devices.Count(d => d.ProposalLineId == l.Id) != l.Quantity))
            throw new BusinessRuleException("Allocate exactly the proposed device quantities.");
        if (request.Devices.Select(d => (d.SensorNodeId, d.UavId, d.GatewayId)).Distinct().Count() != request.Devices.Length) throw new BusinessRuleException("A device cannot be allocated twice.");
        var sensors = request.Devices.Where(x => x.SensorNodeId.HasValue).Select(x => x.SensorNodeId!.Value).ToArray();
        var uavs = request.Devices.Where(x => x.UavId.HasValue).Select(x => x.UavId!.Value).ToArray();
        var gateways = request.Devices.Where(x => x.GatewayId.HasValue).Select(x => x.GatewayId!.Value).ToArray();
        var sensorRows = await db.SensorNodes.Where(x => sensors.Contains(x.Id) && x.IsActive && x.Zone.FarmId == installation.FarmId).ToDictionaryAsync(x => x.Id, ct);
        var uavRows = await db.Uavs.Where(x => uavs.Contains(x.Id) && x.IsActive).ToDictionaryAsync(x => x.Id, ct);
        var gatewayRows = await db.Gateways.Where(x => gateways.Contains(x.Id) && x.IsActive).ToDictionaryAsync(x => x.Id, ct);
        if (sensorRows.Count != sensors.Length || uavRows.Count != uavs.Length || gatewayRows.Count != gateways.Length)
            throw new BusinessRuleException("Choose active registered devices; sensor nodes must belong to this farm.");
        var rows = new List<InstallationDeviceAllocation>();
        foreach (var device in request.Devices)
        {
            string code, name;
            if (device.SensorNodeId is int sensorId)
            {
                var sensor = sensorRows[sensorId];
                code = sensor.DeviceCode; name = sensor.Name;
            }
            else if (device.UavId is int uavId)
            {
                var uav = uavRows[uavId]; code = uav.Code; name = uav.Name;
            }
            else
            {
                var gateway = gatewayRows[device.GatewayId!.Value]; code = gateway.Code; name = gateway.Name;
            }
            rows.Add(new() { JobId = jobId, ProposalLineId = device.ProposalLineId, SensorNodeId = device.SensorNodeId, UavId = device.UavId, GatewayId = device.GatewayId,
                Code = code, Name = name, AllocatedByUserId = actorId, AllocatedAt = now });
        }
        if (await db.InstallationDeviceAllocations.AnyAsync(x => x.ReleasedAt == null && x.JobId != jobId &&
            (x.SensorNodeId.HasValue && sensors.Contains(x.SensorNodeId.Value) || x.UavId.HasValue && uavs.Contains(x.UavId.Value) || x.GatewayId.HasValue && gateways.Contains(x.GatewayId.Value)), ct))
            throw new ConflictException("A selected device is reserved for another installation.");
        var previous = await db.InstallationDeviceAllocations.Where(x => x.JobId == jobId && x.ReleasedAt == null).ToArrayAsync(ct);
        foreach (var row in previous) row.ReleasedAt = now;
        await db.SaveChangesAsync(ct);
        db.InstallationDeviceAllocations.AddRange(rows); job.Revision++;
        job.History.Add(new() { Action = InstallationJobAction.DEVICES_ALLOCATED, UserId = actorId, TechnicianId = job.TechnicianId, ScheduledAt = job.ScheduledAt, Note = request.Note.Trim(), CreatedAt = now });
        await WorkflowNotifications.AddForUserAsync(db, job.TechnicianId, "Installation devices updated", $"Review allocated devices for installation job #{jobId} before submitting the report.", now, ct);
        try { await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg &&
            pg.ConstraintName is "IX_installation_device_allocations_sensor_node_id" or "IX_installation_device_allocations_uav_id" or "IX_installation_device_allocations_gateway_id")
        {
            throw new ConflictException("A selected device was reserved by another installation. Reload available devices.");
        }
    }
}
