using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Constants;

namespace FarmMonitoring.Application.Features.Farms;

public sealed record FarmAccessScope(int UserId, bool IsAdministrator);
public enum FarmResource { Farm, Zone, SensorNode, SensorChannel, Mission, Alert }
public sealed record ResourceFarm(int? FarmId);
public sealed record ResourceFarmReference(int Id, int? FarmId);

public sealed class FarmAccessService(IFarmAccessRepository repository, IAuthRepository users, ICurrentUser current)
{
    public async Task<FarmAccessScope> GetScopeAsync(CancellationToken ct)
    {
        var id = current.UserId ?? throw new AuthException("Authentication required.");
        var user = await users.GetUserByIdAsync(id, ct);
        if (user is not { IsActive: true }) throw new AccessDeniedException("Access denied.");
        var roles = user.UserRoles.Select(x => x.Role.Name).ToArray();
        if (roles.Contains(RoleNames.FarmAdministrator)) return new(id, true);
        if (!roles.Any(x => x is RoleNames.FarmOwner or RoleNames.FarmEngineer)) throw new AccessDeniedException("Access denied.");
        return new(id, false);
    }

    public Task EnsureFarmAccessAsync(int farmId, CancellationToken ct) => EnsureAsync(FarmResource.Farm, farmId, ct);

    public async Task EnsureAsync(FarmResource resource, int id, CancellationToken ct)
    {
        var scope = await GetScopeAsync(ct);
        var resolved = await repository.ResolveAsync(resource, id, ct) ?? throw new NotFoundException($"{resource} not found.");
        if (!scope.IsAdministrator && (resolved.FarmId is not int farmId || !await repository.IsUserAssignedToFarmAsync(scope.UserId, farmId, ct)))
            throw new AccessDeniedException("Access to this farm is denied.");
    }

    public async Task EnsureManyAsync(FarmResource resource, int[] ids, CancellationToken ct)
    {
        if (ids.Length == 0) return;
        var scope = await GetScopeAsync(ct);
        var resolved = await repository.ResolveManyAsync(resource, ids, ct);
        if (resolved.Length != ids.Distinct().Count()) throw new NotFoundException($"A selected {resource} was not found.");
        if (scope.IsAdministrator) return;
        var allowed = (await repository.GetAccessibleFarmIdsAsync(scope, ct)).ToHashSet();
        if (resolved.Any(x => x.FarmId is not int farm || !allowed.Contains(farm))) throw new AccessDeniedException("Access to this farm is denied.");
    }

    public async Task CheckFilterAsync(FarmResource resource, int? id, CancellationToken ct)
    {
        if (id.HasValue) await EnsureAsync(resource, id.Value, ct);
    }

    public async Task<int[]> GetAccessibleFarmIdsAsync(CancellationToken ct) =>
        await repository.GetAccessibleFarmIdsAsync(await GetScopeAsync(ct), ct);

    public async Task EnsureAdministratorAsync(CancellationToken ct)
    {
        if (!(await GetScopeAsync(ct)).IsAdministrator) throw new AccessDeniedException("Administrator permission required.");
    }
}
