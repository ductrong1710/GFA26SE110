using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FarmMonitoring.Infrastructure.Persistence.Repositories;

// Shared with mission transitions and equipment edits. A transaction-scoped lock
// serializes reservation decisions, including two different gateways sharing a UAV.
public static class FlightReservation
{
    public static Task LockAsync(AppDbContext db, CancellationToken ct) =>
        db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(74611001)", ct);
    public static IQueryable<FlightTest> Active(AppDbContext db, DateTimeOffset now) =>
        db.FlightTests.Where(x => x.Status == FlightTestStatus.CLAIMED || x.Status == FlightTestStatus.RECOVERY_REQUIRED ||
            (x.Status == FlightTestStatus.QUEUED && x.ExpiresAt > now));
}

public sealed class FlightTestRepository(AppDbContext db) : IFlightTestRepository
{
    public async Task<T> InTransactionAsync<T>(Func<Task<T>> action, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await FlightReservation.LockAsync(db, ct);
        var result = await action();
        await transaction.CommitAsync(ct);
        return result;
    }
    public Task<FlightTest?> FindAsync(int id, CancellationToken ct) => db.FlightTests.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<FlightTest?> FindRequestAsync(int gatewayId, string key, CancellationToken ct) =>
        db.FlightTests.SingleOrDefaultAsync(x => x.GatewayId == gatewayId && x.RequestKey == key, ct);
    public Task<FlightTest?> NextAsync(int gatewayId, DateTimeOffset now, CancellationToken ct) =>
        db.FlightTests.AsNoTracking().Where(x => x.GatewayId == gatewayId && x.Status == FlightTestStatus.QUEUED && x.ExpiresAt > now)
            .OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
    public Task<Gateway?> GatewayAsync(int id, CancellationToken ct) => db.Gateways.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<Uav?> UavAsync(int id, CancellationToken ct) => db.Uavs.SingleOrDefaultAsync(x => x.Id == id, ct);
    public async Task<bool> HasConflictAsync(int gatewayId, int uavId, int excludingId, DateTimeOffset now, CancellationToken ct) =>
        await FlightReservation.Active(db, now).AnyAsync(x => x.Id != excludingId && (x.GatewayId == gatewayId || x.UavId == uavId), ct) ||
        await db.Missions.AnyAsync(x => (x.GatewayId == gatewayId || x.UavId == uavId) &&
            (x.Status == MissionStatus.SCHEDULED || x.Status == MissionStatus.RUNNING), ct);
    public void Add(FlightTest test) => db.FlightTests.Add(test);
    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
