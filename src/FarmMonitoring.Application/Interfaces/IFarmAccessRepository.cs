using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Farms;

namespace FarmMonitoring.Application.Interfaces;

public interface IFarmAccessRepository
{
    Task<bool> IsUserAssignedToFarmAsync(int userId, int farmId, CancellationToken ct);
    Task<int[]> GetAccessibleFarmIdsAsync(FarmAccessScope scope, CancellationToken ct);
    Task<ResourceFarmReference[]> ResolveManyAsync(FarmResource resource, int[] ids, CancellationToken ct);
    Task<ResourceFarm?> ResolveAsync(FarmResource resource, int id, CancellationToken ct);
    Task<PagedResult<FarmMemberResponse>> MembersAsync(int farmId, PageQuery query, CancellationToken ct);
    Task<FarmMemberResponse> AssignAsync(int farmId, int userId, DateTimeOffset now, CancellationToken ct);
    Task<bool> RemoveAsync(int farmId, int userId, CancellationToken ct);
}
