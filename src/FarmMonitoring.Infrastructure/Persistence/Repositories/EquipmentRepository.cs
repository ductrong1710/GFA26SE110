using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Equipment;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using FarmMonitoring.Domain.Constants;

namespace FarmMonitoring.Infrastructure.Persistence.Repositories;

public sealed class EquipmentRepository(AppDbContext db) : IEquipmentRepository
{
    private async Task<bool> IsAdminAsync(int actorId, CancellationToken ct)
    {
        var roles = await db.UserRoles.Where(x => x.UserId == actorId && x.User.IsActive).Select(x => x.Role.Name).ToArrayAsync(ct);
        if (!roles.Contains(RoleNames.FarmAdministrator) && !roles.Contains(RoleNames.FarmOwner)) throw new AccessDeniedException("Equipment access denied.");
        return roles.Contains(RoleNames.FarmAdministrator);
    }
    private IQueryable<RentalDeviceAssignment> Rentals(int actorId, bool admin) =>
        FarmAccessQuery.RentalAssignments(db, new(actorId, admin)).AsNoTracking();
    public async Task<EquipmentRental?> ReadRentalAsync(int deviceId, bool uav, int actorId, DateTimeOffset now, CancellationToken ct)
    {
        var admin = await IsAdminAsync(actorId, ct);
        var rental = await Rentals(actorId, admin).Where(x => uav ? x.UavId == deviceId : x.GatewayId == deviceId)
            .Select(x => new EquipmentRental(x.Contract.FarmId, x.Contract.StartAt, x.Contract.EndAt,
                x.Contract.Status == RentalStatus.ACTIVE && x.Contract.EndAt <= now ? "EXPIRED" : x.Contract.Status.ToString())).SingleOrDefaultAsync(ct);
        if (!admin && rental is null) throw new AccessDeniedException("This equipment is not assigned to your rental.");
        return rental;
    }
    public async Task<PagedResult<UavResponse>> ListUavsAsync(PageQuery query, int actorId, DateTimeOffset now, CancellationToken ct)
    {
        var admin = await IsAdminAsync(actorId, ct);
        var rentals = Rentals(actorId, admin);
        var rows = db.Uavs.AsNoTracking();
        if (!admin) rows = rows.Where(x => rentals.Any(r => r.UavId == x.Id));
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            rows = rows.Where(x => x.Code.ToLower().Contains(term) || x.Name.ToLower().Contains(term));
        }
        var count = await rows.CountAsync(ct);
        var items = await rows.OrderBy(x => x.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new UavResponse(x.Id, x.Code, x.Name, x.Model, x.Status, x.BatteryPercent, x.LastSeenAt,
                x.IsActive, x.CreatedAt, x.UpdatedAt, rentals.Where(r => r.UavId == x.Id).Select(r => new EquipmentRental(r.Contract.FarmId,
                    r.Contract.StartAt, r.Contract.EndAt, r.Contract.Status == RentalStatus.ACTIVE && r.Contract.EndAt <= now ? "EXPIRED" : r.Contract.Status.ToString())).FirstOrDefault())).ToArrayAsync(ct);
        return new(items, count, query.Page, query.PageSize);
    }
    public async Task<PagedResult<GatewayResponse>> ListGatewaysAsync(PageQuery query, int actorId, DateTimeOffset now, CancellationToken ct)
    {
        var admin = await IsAdminAsync(actorId, ct);
        var rentals = Rentals(actorId, admin);
        var rows = db.Gateways.AsNoTracking();
        if (!admin) rows = rows.Where(x => rentals.Any(r => r.GatewayId == x.Id));
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            rows = rows.Where(x => x.Code.ToLower().Contains(term) || x.Name.ToLower().Contains(term));
        }
        var count = await rows.CountAsync(ct);
        var items = await rows.OrderBy(x => x.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new GatewayResponse(x.Id, x.Code, x.Name, x.GatewayType, x.Status, x.UavId, x.BatteryPercent,
                x.LastSeenAt, x.FirmwareVersion, x.IsActive, x.CreatedAt, x.UpdatedAt, rentals.Where(r => r.GatewayId == x.Id).Select(r => new EquipmentRental(r.Contract.FarmId,
                    r.Contract.StartAt, r.Contract.EndAt, r.Contract.Status == RentalStatus.ACTIVE && r.Contract.EndAt <= now ? "EXPIRED" : r.Contract.Status.ToString())).FirstOrDefault())).ToArrayAsync(ct);
        return new(items, count, query.Page, query.PageSize);
    }
    public Task<Uav?> FindUavAsync(int id, CancellationToken ct) => db.Uavs.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<Gateway?> FindGatewayAsync(int id, CancellationToken ct) => db.Gateways.SingleOrDefaultAsync(x => x.Id == id, ct);
    public void AddUav(Uav uav) => db.Uavs.Add(uav);
    public void AddGateway(Gateway gateway) => db.Gateways.Add(gateway);
    // Contact updates do not edit equipment or interfere with flight reservations.
    public async Task<bool> RecordHeartbeatAsync(int id, DateTimeOffset receivedAt, CancellationToken ct) =>
        await db.Gateways.Where(x => x.Id == id && x.IsActive)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.LastSeenAt,
                x => x.LastSeenAt == null || x.LastSeenAt < receivedAt ? receivedAt : x.LastSeenAt), ct) == 1;
    public async Task SaveAsync(CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await FlightReservation.LockAsync(db, ct);
        // Read the reservations under the same lock used by create/claim/mission transitions.
        foreach (var entry in db.ChangeTracker.Entries<Gateway>().Where(x => x.State is EntityState.Modified or EntityState.Added))
            if (await FlightReservation.Active(db, DateTimeOffset.UtcNow).AnyAsync(x => x.GatewayId == entry.Entity.Id || x.UavId == entry.Entity.UavId, ct))
                throw new ConflictException("Gateway or assigned UAV is reserved by a flight test.");
        foreach (var entry in db.ChangeTracker.Entries<Uav>().Where(x => x.State == EntityState.Modified))
            if (await FlightReservation.Active(db, DateTimeOffset.UtcNow).AnyAsync(x => x.UavId == entry.Entity.Id, ct))
                throw new ConflictException("UAV is reserved by a flight test.");
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg
            && pg.ConstraintName is "IX_uavs_code" or "IX_gateways_code")
        { throw new ConflictException("The equipment code is already registered."); }
        await transaction.CommitAsync(ct);
    }
}
