using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Interfaces;
using FluentValidation;

namespace FarmMonitoring.Application.Features.Farms;

public sealed record FarmMemberRequest(int UserId);
public sealed record FarmMemberResponse(int UserId, string Email, string FullName, string[] Roles, DateTimeOffset AssignedAt);

public sealed class FarmMembershipService(IFarmAccessRepository repository, FarmAccessService access, IValidator<PageQuery> pages, TimeProvider clock)
{
    public async Task<PagedResult<FarmMemberResponse>> ListAsync(int farmId, PageQuery query, CancellationToken ct)
    {
        await access.EnsureAdministratorAsync(ct);
        await access.EnsureFarmAccessAsync(farmId, ct);
        await pages.ValidateAndThrowAsync(query, ct);
        return await repository.MembersAsync(farmId, query, ct);
    }
    public async Task<FarmMemberResponse> AssignAsync(int farmId, FarmMemberRequest request, CancellationToken ct)
    {
        await access.EnsureAdministratorAsync(ct);
        if (request.UserId <= 0) throw new ValidationException("A positive userId is required.");
        await access.EnsureFarmAccessAsync(farmId, ct);
        return await repository.AssignAsync(farmId, request.UserId, clock.GetUtcNow(), ct);
    }
    public async Task RemoveAsync(int farmId, int userId, CancellationToken ct)
    {
        await access.EnsureAdministratorAsync(ct);
        await access.EnsureFarmAccessAsync(farmId, ct);
        if (!await repository.RemoveAsync(farmId, userId, ct)) throw new NotFoundException("Farm membership not found.");
    }
}
