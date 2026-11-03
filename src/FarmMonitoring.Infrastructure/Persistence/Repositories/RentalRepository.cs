using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Rentals;
using FarmMonitoring.Application.Features.Consultations;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Constants;
using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FarmMonitoring.Infrastructure.Persistence.Repositories;
public sealed class RentalRepository(AppDbContext db) : IRentalRepository
{
    private async Task<IQueryable<RentalContract>> ScopeAsync(int actor, CancellationToken ct)
    {
        var roles = await db.UserRoles.Where(x => x.UserId == actor && x.User.IsActive).Select(x => x.Role.Name).ToArrayAsync(ct);
        var admin = roles.Contains(RoleNames.FarmAdministrator); var engineer = roles.Contains(RoleNames.FarmEngineer);
        if (!admin && !engineer && !roles.Contains(RoleNames.FarmOwner)) throw new AccessDeniedException("Rental access denied.");
        var farms = FarmAccessQuery.AssignedFarmIds(db, actor);
        return db.RentalContracts.AsNoTracking().Where(x => admin || x.OwnerId == actor && farms.Contains(x.FarmId) ||
            engineer && x.InstallationJob.InstallationRequest.Proposal.Consultation.EngineerId == actor);
    }
    public async Task<PagedResult<RentalSummary>> ListAsync(int actorId, PageQuery query, DateTimeOffset now, CancellationToken ct)
    {
        var rows = await ScopeAsync(actorId, ct); var count = await rows.CountAsync(ct);
        var items = await rows.OrderByDescending(x => x.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new RentalSummary(x.Id, x.InstallationJobId, x.FarmId, x.OwnerId, x.StartAt, x.EndAt,
                x.Status == RentalStatus.ACTIVE && x.EndAt <= now ? "EXPIRED" : x.Status.ToString(), x.Revision)).ToArrayAsync(ct);
        return new(items, count, query.Page, query.PageSize);
    }
    public async Task<RentalDetail> GetAsync(int id, int actorId, DateTimeOffset now, CancellationToken ct)
    {
        var rows = await ScopeAsync(actorId, ct);
        var row = await rows.Include(x => x.Devices).Include(x => x.History).Include(x => x.InstallationJob).ThenInclude(x => x.InstallationRequest).ThenInclude(x => x.Proposal).ThenInclude(x => x.Lines).AsSplitQuery().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (row is null)
        {
            if (await db.RentalContracts.AnyAsync(x => x.Id == id, ct)) throw new AccessDeniedException("Access to this rental is denied.");
            throw new NotFoundException("Rental contract not found.");
        }
        var proposal = row.InstallationJob.InstallationRequest.Proposal;
        var paper = await db.InstallationReports.Where(x => x.JobId == row.InstallationJobId && x.Approved == true).OrderByDescending(x => x.Revision).Select(x => x.PaperworkJson).FirstOrDefaultAsync(ct);
        return new(row.Id, row.InstallationJobId, row.FarmId, row.OwnerId, row.StartAt, row.EndAt, row.EffectiveStatus(now).ToString(), row.Revision,
            row.Devices.OrderBy(x => x.Id).Select(x => new RentalDeviceResponse(x.Id, x.SensorNodeId, x.UavId, x.GatewayId, x.Code, x.Name, x.InstallationLocation, x.ReturnedAt)).ToArray(),
            row.History.OrderBy(x => x.Id).Select(x => new RentalHistoryResponse(x.Id, x.Action, x.UserId, x.Note, x.CreatedAt, x.StartAt, x.EndAt)).ToArray(),
            proposal.Lines.OrderBy(x=>x.Id).Select(x=>new ProposalLineResponse(x.Id,x.SurveyZoneId,x.DeviceType,x.Quantity,x.InstallationLocation,x.PublicContentId,x.PriceRevision,x.MonthlyRentVnd,x.ProductName)).ToArray(),
            proposal.Lines.Any(x=>x.MonthlyRentVnd == null) ? null : proposal.Lines.Sum(x=>x.MonthlyRentVnd!.Value*x.Quantity)*proposal.RentalMonths,paper);
    }
    public async Task<int> CreateAsync(int jobId, int actorId, CreateRentalRequest request, DateTimeOffset now, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await FlightReservation.LockAsync(db, ct);
        var id = await CreateWithinAsync(jobId,actorId,request,now,ct);
        await transaction.CommitAsync(ct); return id;
    }
    internal async Task<int> CreateWithinAsync(int jobId,int actorId,CreateRentalRequest request,DateTimeOffset now,CancellationToken ct)
    {
        await LockAdminAsync(actorId, ct);
        var job = await db.InstallationJobs.FromSqlInterpolated($"SELECT * FROM installation_jobs WHERE id = {jobId} FOR UPDATE").SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException("Installation job not found.");
        if (job.Status != InstallationJobStatus.APPROVED) throw new ConflictException("Approve installation before creating a rental.");
        if (await db.RentalContracts.AnyAsync(x => x.InstallationJobId == jobId, ct)) throw new ConflictException("This installation already has a rental contract.");
        var installation = await db.InstallationRequests.SingleAsync(x => x.Id == job.InstallationRequestId, ct);
        var report = await db.InstallationReports.Include(x => x.Devices).Where(x => x.JobId == jobId).OrderByDescending(x => x.Revision).FirstAsync(ct);
        if (report.Approved != true || report.PaperworkJson is null && report.ConfirmedAt is null) throw new ConflictException("Confirmed and approved installation evidence is required.");
        if (request.StartAt != OperationalRules.RentalStart(report.CompletedAt ?? report.CreatedAt))
            throw new BusinessRuleException("Rental must start at midnight Vietnam time on the day after completed installation handover.");
        var allocations = await db.InstallationDeviceAllocations.Where(x => x.JobId == jobId && x.ReleasedAt == null).ToArrayAsync(ct);
        if (allocations.Length == 0 || allocations.Length != report.Devices.Count) throw new ConflictException("Installation device allocation no longer matches approval.");
        var rental = new RentalContract { InstallationJobId = jobId, FarmId = installation.FarmId, OwnerId = installation.OwnerId,
            StartAt = request.StartAt, EndAt = request.EndAt, Status = RentalStatus.DRAFT, CreatedAt = now };
        foreach (var allocation in allocations)
        {
            var check = report.Devices.SingleOrDefault(x => x.ProposalLineId == allocation.ProposalLineId && x.SensorNodeId == allocation.SensorNodeId && x.UavId == allocation.UavId && x.GatewayId == allocation.GatewayId);
            if (check is null) throw new ConflictException("Approved report must match the allocated devices.");
            rental.Devices.Add(new() { InstallationAllocationId = allocation.Id, SensorNodeId = allocation.SensorNodeId, UavId = allocation.UavId,
                GatewayId = allocation.GatewayId, Code = allocation.Code, Name = allocation.Name, InstallationLocation = check.InstallationLocation });
        }
        await EnsureEligibleAsync(rental, ct);
        History(rental, "CREATED", actorId, request.Note, now); db.RentalContracts.Add(rental);
        await db.SaveChangesAsync(ct); return rental.Id;
    }
    public async Task ChangeAsync(int id, int actorId, string action, int expectedRevision, string note, DateTimeOffset? endAt, DateTimeOffset now, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await FlightReservation.LockAsync(db, ct);
        await LockAdminAsync(actorId, ct);
        var rental = await db.RentalContracts.FromSqlInterpolated($"SELECT * FROM rental_contracts WHERE id = {id} FOR UPDATE").SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException("Rental contract not found.");
        await db.Entry(rental).Collection(x => x.Devices).LoadAsync(ct);
        if (rental.Revision != expectedRevision) throw new ConflictException("Rental changed. Reload the latest revision.");
        if (rental.Status == RentalStatus.ENDED) throw new ConflictException("Ended contracts cannot be changed.");
        if (action == "ACTIVATED")
        {
            if (rental.Status != RentalStatus.DRAFT || rental.StartAt > now || rental.EndAt <= now) throw new ConflictException("Activation requires a draft within its agreed rental period.");
            await EnsureEligibleAsync(rental, ct); rental.Status = RentalStatus.ACTIVE;
        }
        else if (action == "EXTENDED")
        {
            if (rental.Status is not (RentalStatus.ACTIVE or RentalStatus.EXPIRED) || endAt is null || endAt <= rental.EndAt || endAt <= now)
                throw new ConflictException("Extend an active or expired rental to a later confirmed end date.");
            await EnsureEligibleAsync(rental, ct); rental.EndAt = endAt.Value; rental.WarnedForEndAt = null; rental.Status = RentalStatus.ACTIVE;
        }
        else if (action == "ENDED")
        {
            var uavs = rental.Devices.Where(x => x.ReturnedAt == null && x.UavId.HasValue).Select(x => x.UavId!.Value).ToArray();
            var gateways = rental.Devices.Where(x => x.ReturnedAt == null && x.GatewayId.HasValue).Select(x => x.GatewayId!.Value).ToArray();
            if (await db.Missions.AnyAsync(x => x.Status == MissionStatus.RUNNING &&
                (x.UavId.HasValue && uavs.Contains(x.UavId.Value) || x.GatewayId.HasValue && gateways.Contains(x.GatewayId.Value)), ct) ||
                await FlightReservation.Active(db, now).AnyAsync(x => gateways.Contains(x.GatewayId) || uavs.Contains(x.UavId), ct))
                throw new ConflictException("Finish active flight operations before recording equipment retrieval.");
            var allocationIds = rental.Devices.Where(x => x.ReturnedAt == null).Select(x => x.InstallationAllocationId).ToArray();
            var allocations = await db.InstallationDeviceAllocations.Where(x => allocationIds.Contains(x.Id)).ToArrayAsync(ct);
            foreach (var device in rental.Devices.Where(x => x.ReturnedAt == null)) device.ReturnedAt = now;
            foreach (var allocation in allocations.Where(x => x.ReleasedAt == null)) allocation.ReleasedAt = now;
            rental.Status = RentalStatus.ENDED;
        }
        else throw new BusinessRuleException("Unsupported rental action.");
        rental.Revision++; History(rental, action, actorId, note, now);
        await NotifyAsync(rental, $"Rental {action.ToLowerInvariant()}", now, ct);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
    }
    public async Task MonitorAsync(DateTimeOffset now, int warningDays, int batchSize, CancellationToken ct)
    {
        var warningAt = now.AddDays(warningDays);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await FlightReservation.LockAsync(db, ct);
        var starting = await db.RentalContracts.FromSqlInterpolated($"SELECT * FROM rental_contracts WHERE status = 'DRAFT' AND start_at <= {now} AND end_at > {now} ORDER BY id LIMIT {batchSize} FOR UPDATE SKIP LOCKED").ToArrayAsync(ct);
        foreach (var rental in starting)
        {
            await db.Entry(rental).Collection(x => x.Devices).LoadAsync(ct);
            await EnsureEligibleAsync(rental, ct);
            rental.Status = RentalStatus.ACTIVE; rental.Revision++;
            History(rental, "ACTIVATED", null, "Automatically activated at agreed rental start.", now);
            await NotifyAsync(rental, "Rental activated", now, ct);
        }
        var due = await db.RentalContracts.FromSqlInterpolated($"SELECT * FROM rental_contracts WHERE status = 'ACTIVE' AND end_at <= {warningAt} AND (end_at <= {now} OR warned_for_end_at IS DISTINCT FROM end_at) ORDER BY end_at, id LIMIT {batchSize} FOR UPDATE SKIP LOCKED").ToArrayAsync(ct);
        foreach (var rental in due)
        {
            if (rental.EndAt <= now)
            {
                rental.Status = RentalStatus.EXPIRED; rental.Revision++;
                History(rental, "EXPIRED", null, "Rental period expired; devices remain reserved until recorded retrieval.", now);
                await NotifyAsync(rental, "Rental expired", now, ct);
            }
            else
            {
                rental.WarnedForEndAt = rental.EndAt;
                await NotifyAsync(rental, "Rental expiring soon", now, ct);
            }
        }
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
    }
    private async Task LockAdminAsync(int actorId, CancellationToken ct)
    {
        await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id = {actorId} FOR UPDATE").ToArrayAsync(ct);
        if (!await db.UserRoles.AnyAsync(x => x.UserId == actorId && x.User.IsActive && x.Role.Name == RoleNames.FarmAdministrator, ct))
            throw new AccessDeniedException("Administrator access required.");
    }
    private async Task EnsureEligibleAsync(RentalContract rental, CancellationToken ct)
    {
        if (!await db.Farms.AnyAsync(x => x.Id == rental.FarmId && x.IsActive, ct) || !await FarmAccessQuery.AssignedFarmIds(db, rental.OwnerId).ContainsAsync(rental.FarmId, ct))
            throw new BusinessRuleException("An active farm and current linked Farm Owner are required.");
        if (!rental.Devices.Any(x => x.ReturnedAt == null)) throw new BusinessRuleException("Rental devices must still be assigned.");
        var ids = rental.Devices.Where(x => x.ReturnedAt == null).Select(x => x.InstallationAllocationId).ToArray();
        if (await db.InstallationDeviceAllocations.CountAsync(x => ids.Contains(x.Id) && x.ReleasedAt == null, ct) != ids.Length)
            throw new BusinessRuleException("Device reservations are no longer valid.");
        var sensors = rental.Devices.Where(x => x.ReturnedAt == null && x.SensorNodeId.HasValue).Select(x => x.SensorNodeId!.Value).ToArray();
        var uavs = rental.Devices.Where(x => x.ReturnedAt == null && x.UavId.HasValue).Select(x => x.UavId!.Value).ToArray();
        var gateways = rental.Devices.Where(x => x.ReturnedAt == null && x.GatewayId.HasValue).Select(x => x.GatewayId!.Value).ToArray();
        if (await db.SensorNodes.CountAsync(x => sensors.Contains(x.Id) && x.IsActive && x.Zone.FarmId == rental.FarmId, ct) != sensors.Length ||
            await db.Uavs.CountAsync(x => uavs.Contains(x.Id) && x.IsActive, ct) != uavs.Length || await db.Gateways.CountAsync(x => gateways.Contains(x.Id) && x.IsActive, ct) != gateways.Length)
            throw new BusinessRuleException("Rental devices must be active and sensor nodes must remain in the assigned farm.");
    }
    private async Task NotifyAsync(RentalContract rental, string subject, DateTimeOffset now, CancellationToken ct)
    {
        var message = $"Rental contract #{rental.Id} for farm #{rental.FarmId}; confirmed end {rental.EndAt:O}.";
        if (await FarmAccessQuery.AssignedFarmIds(db, rental.OwnerId).ContainsAsync(rental.FarmId, ct)) await WorkflowNotifications.AddForUserAsync(db, rental.OwnerId, subject, message, now, ct);
        await WorkflowNotifications.AddForRoleAsync(db, RoleNames.FarmAdministrator, subject, message, now, ct);
    }
    private static void History(RentalContract rental, string action, int? userId, string note, DateTimeOffset now) =>
        rental.History.Add(new() { Action = action, UserId = userId, Note = note.Trim(), CreatedAt = now, StartAt = rental.StartAt, EndAt = rental.EndAt });
}
