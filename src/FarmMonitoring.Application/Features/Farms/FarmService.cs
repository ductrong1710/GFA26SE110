using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Entities;
using FluentValidation;

namespace FarmMonitoring.Application.Features.Farms;

public sealed class FarmService(FarmAccessService access, IFarmRepository repository, TimeProvider clock, IValidator<FarmRequest> farmValidator,
    IValidator<ZoneRequest> zoneValidator, IValidator<FarmStatusRequest> statusValidator, IValidator<PageQuery> pageValidator)
{
    public async Task<PagedResult<FarmResponse>> ListFarmsAsync(PageQuery query, CancellationToken ct)
    {
        await pageValidator.ValidateAndThrowAsync(query, ct);
        return await repository.ListFarmsAsync(query, await access.GetScopeAsync(ct), ct);
    }

    public async Task<PagedResult<ZoneResponse>> ListZonesAsync(int farmId, PageQuery query, CancellationToken ct)
    {
        await pageValidator.ValidateAndThrowAsync(query, ct);
        await RequireFarmAsync(farmId, ct);
        return await repository.ListZonesAsync(farmId, query, ct);
    }

    public async Task<FarmResponse> GetFarmAsync(int id, CancellationToken ct) => ToResponse(await RequireFarmAsync(id, ct));
    public async Task<ZoneResponse> GetZoneAsync(int id, CancellationToken ct) => ToResponse(await RequireZoneAsync(id, ct));

    public async Task<FarmResponse> CreateFarmAsync(FarmRequest request, CancellationToken ct)
    {
        await farmValidator.ValidateAndThrowAsync(request, ct);
        var farm = new Farm { CreatedAt = clock.GetUtcNow() };
        Apply(farm, request);
        var scope = await access.GetScopeAsync(ct);
        if (!scope.IsAdministrator) farm.UserFarms.Add(new UserFarm { UserId = scope.UserId, CreatedAt = farm.CreatedAt });
        // EF commits the farm and creator assignment in the same SaveChanges transaction.
        repository.AddFarm(farm);
        await repository.SaveAsync(ct);
        return ToResponse(farm);
    }

    public async Task<FarmResponse> UpdateFarmAsync(int id, FarmRequest request, CancellationToken ct)
    {
        await farmValidator.ValidateAndThrowAsync(request, ct);
        var farm = await RequireFarmAsync(id, ct);
        Apply(farm, request);
        farm.UpdatedAt = clock.GetUtcNow();
        await repository.SaveAsync(ct);
        return ToResponse(farm);
    }

    public async Task<FarmResponse> SetFarmStatusAsync(int id, FarmStatusRequest request, CancellationToken ct)
    {
        await statusValidator.ValidateAndThrowAsync(request, ct);
        var farm = await RequireFarmAsync(id, ct);
        farm.IsActive = request.IsActive!.Value;
        farm.UpdatedAt = clock.GetUtcNow();
        await repository.SaveAsync(ct);
        return ToResponse(farm);
    }

    public async Task<ZoneResponse> CreateZoneAsync(int farmId, ZoneRequest request, CancellationToken ct)
    {
        await zoneValidator.ValidateAndThrowAsync(request, ct);
        await RequireFarmAsync(farmId, ct);
        var zone = new Zone { FarmId = farmId, CreatedAt = clock.GetUtcNow() };
        Apply(zone, request);
        repository.AddZone(zone);
        await repository.SaveAsync(ct);
        return ToResponse(zone);
    }

    public async Task<ZoneResponse> UpdateZoneAsync(int id, ZoneRequest request, CancellationToken ct)
    {
        await zoneValidator.ValidateAndThrowAsync(request, ct);
        var zone = await RequireZoneAsync(id, ct);
        Apply(zone, request);
        zone.UpdatedAt = clock.GetUtcNow();
        await repository.SaveAsync(ct);
        return ToResponse(zone);
    }

    private async Task<Farm> RequireFarmAsync(int id, CancellationToken ct)
    {
        await access.EnsureAsync(FarmResource.Farm, id, ct);
        return await repository.FindFarmAsync(id, ct) ?? throw new NotFoundException("Farm not found.");
    }
    private async Task<Zone> RequireZoneAsync(int id, CancellationToken ct)
    {
        await access.EnsureAsync(FarmResource.Zone, id, ct);
        return await repository.FindZoneAsync(id, ct) ?? throw new NotFoundException("Zone not found.");
    }
    private static void Apply(Farm farm, FarmRequest request)
    {
        farm.Name = request.Name.Trim();
        farm.Description = request.Description;
        farm.Latitude = request.Latitude;
        farm.Longitude = request.Longitude;
    }
    private static void Apply(Zone zone, ZoneRequest request)
    {
        zone.Name = request.Name.Trim();
        zone.Description = request.Description;
        zone.CenterLatitude = request.CenterLatitude;
        zone.CenterLongitude = request.CenterLongitude;
    }
    private static FarmResponse ToResponse(Farm x) => new(x.Id, x.Name, x.Description, x.Latitude, x.Longitude, x.IsActive, x.CreatedAt, x.UpdatedAt);
    private static ZoneResponse ToResponse(Zone x) => new(x.Id, x.FarmId, x.Name, x.Description, x.CenterLatitude, x.CenterLongitude, x.CreatedAt, x.UpdatedAt);
}
