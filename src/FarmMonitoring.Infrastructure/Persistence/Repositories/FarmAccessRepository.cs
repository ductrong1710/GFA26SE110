using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Farms;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Constants;
using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FarmMonitoring.Infrastructure.Persistence.Repositories;

public sealed class FarmAccessRepository(AppDbContext db) : IFarmAccessRepository
{
    public Task<bool> IsUserAssignedToFarmAsync(int userId, int farmId, CancellationToken ct) =>
        FarmAccessQuery.AssignedFarmIds(db, userId).ContainsAsync(farmId, ct);

    public Task<int[]> GetAccessibleFarmIdsAsync(FarmAccessScope scope, CancellationToken ct) =>
        (scope.IsAdministrator ? db.Farms.Select(x => x.Id) : FarmAccessQuery.AssignedFarmIds(db, scope.UserId)).Order().ToArrayAsync(ct);

    public Task<ResourceFarmReference[]> ResolveManyAsync(FarmResource resource, int[] ids, CancellationToken ct) => resource switch
    {
        FarmResource.Zone => db.Zones.Where(x => ids.Contains(x.Id)).Select(x => new ResourceFarmReference(x.Id, x.FarmId)).ToArrayAsync(ct),
        FarmResource.SensorNode => db.SensorNodes.Where(x => ids.Contains(x.Id)).Select(x => new ResourceFarmReference(x.Id, x.Zone.FarmId)).ToArrayAsync(ct),
        _ => throw new ArgumentOutOfRangeException(nameof(resource))
    };

    public Task<ResourceFarm?> ResolveAsync(FarmResource resource, int id, CancellationToken ct) => resource switch
    {
        FarmResource.Farm => db.Farms.Where(x => x.Id == id).Select(x => new ResourceFarm(x.Id)).SingleOrDefaultAsync(ct),
        FarmResource.Zone => db.Zones.Where(x => x.Id == id).Select(x => new ResourceFarm(x.FarmId)).SingleOrDefaultAsync(ct),
        FarmResource.SensorNode => db.SensorNodes.Where(x => x.Id == id).Select(x => new ResourceFarm(x.Zone.FarmId)).SingleOrDefaultAsync(ct),
        FarmResource.SensorChannel => db.SensorChannels.Where(x => x.Id == id).Select(x => new ResourceFarm(x.SensorNode.Zone.FarmId)).SingleOrDefaultAsync(ct),
        FarmResource.Mission => db.Missions.Where(x => x.Id == id).Select(x => new ResourceFarm(x.FarmId)).SingleOrDefaultAsync(ct),
        FarmResource.Alert => db.Alerts.Where(x => x.Id == id).Select(FarmAccessQuery.AlertFarm).Select(x => new ResourceFarm(x)).SingleOrDefaultAsync(ct),
        _ => throw new ArgumentOutOfRangeException(nameof(resource))
    };

    public async Task<PagedResult<FarmMemberResponse>> MembersAsync(int farmId, PageQuery query, CancellationToken ct)
    {
        var rows = db.UserFarms.AsNoTracking().Where(x => x.FarmId == farmId);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            rows = rows.Where(x => x.User.Email.Contains(search) || x.User.FullName.Contains(search));
        }
        var count = await rows.CountAsync(ct);
        var result = await rows.OrderBy(x => x.UserId).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new FarmMemberResponse(x.UserId, x.User.Email, x.User.FullName,
                x.User.UserRoles.OrderBy(r => r.Role.Name).Select(r => r.Role.Name).ToArray(), x.CreatedAt)).ToArrayAsync(ct);
        return new(result, count, query.Page, query.PageSize);
    }

    public async Task<FarmMemberResponse> AssignAsync(int farmId, int userId, DateTimeOffset now, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // Same user-row lock as user status/role mutations: eligibility cannot change during assignment.
        var user = await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id = {userId} FOR UPDATE").SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException("User not found.");
        var roles = await db.UserRoles.Where(x => x.UserId == userId).Select(x => x.Role.Name).Order().ToArrayAsync(ct);
        if (!user.IsActive || roles.Contains(RoleNames.FarmAdministrator) || !roles.Any(x => x is RoleNames.FarmOwner or RoleNames.FarmEngineer))
            throw new BusinessRuleException("Membership requires an active FarmOwner or FarmEngineer without FarmAdministrator role.");
        db.UserFarms.Add(new UserFarm { UserId = userId, FarmId = farmId, CreatedAt = now });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { throw new ConflictException("User is already assigned to this farm."); }
        await transaction.CommitAsync(ct);
        return new(user.Id, user.Email, user.FullName, roles, now);
    }

    public async Task<bool> RemoveAsync(int farmId, int userId, CancellationToken ct) =>
        await db.UserFarms.Where(x => x.FarmId == farmId && x.UserId == userId).ExecuteDeleteAsync(ct) != 0;
}
