using FarmMonitoring.Api.Authorization;
using FarmMonitoring.Api.Contracts;
using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Farms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmMonitoring.Api.Controllers;

[ApiController]
[Route("api/farms")]
[Authorize(Policy = AccessPolicies.ReadFarmData)]
[Produces("application/json")]
[ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ApiError>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
public sealed class FarmsController(FarmService farms) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiPageResponse<FarmResponse>>> List([FromQuery] PageQuery query, CancellationToken ct) =>
        Ok(ApiPageResponse<FarmResponse>.From(await farms.ListFarmsAsync(query, ct)));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<FarmResponse>>> Get(int id, CancellationToken ct) =>
        Ok(ApiResponse<FarmResponse>.Ok(await farms.GetFarmAsync(id, ct)));

    [HttpPost]
    [Authorize(Policy = AccessPolicies.ManageFarms)]
    [ProducesResponseType<ApiResponse<FarmResponse>>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<FarmResponse>>> Create(FarmRequest request, CancellationToken ct)
    {
        var farm = await farms.CreateFarmAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = farm.Id }, ApiResponse<FarmResponse>.Ok(farm));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = AccessPolicies.ManageFarms)]
    public async Task<ActionResult<ApiResponse<FarmResponse>>> Update(int id, FarmRequest request, CancellationToken ct) =>
        Ok(ApiResponse<FarmResponse>.Ok(await farms.UpdateFarmAsync(id, request, ct)));

    [HttpPatch("{id:int}/status")]
    [Authorize(Policy = AccessPolicies.ManageFarms)]
    public async Task<ActionResult<ApiResponse<FarmResponse>>> Status(int id, FarmStatusRequest request, CancellationToken ct) =>
        Ok(ApiResponse<FarmResponse>.Ok(await farms.SetFarmStatusAsync(id, request, ct)));

    [HttpGet("{farmId:int}/zones")]
    public async Task<ActionResult<ApiPageResponse<ZoneResponse>>> Zones(int farmId, [FromQuery] PageQuery query, CancellationToken ct) =>
        Ok(ApiPageResponse<ZoneResponse>.From(await farms.ListZonesAsync(farmId, query, ct)));

    [HttpPost("{farmId:int}/zones")]
    [Authorize(Policy = AccessPolicies.ManageFarms)]
    [ProducesResponseType<ApiResponse<ZoneResponse>>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<ZoneResponse>>> CreateZone(int farmId, ZoneRequest request, CancellationToken ct)
    {
        var zone = await farms.CreateZoneAsync(farmId, request, ct);
        return CreatedAtAction(nameof(ZonesController.Get), "Zones", new { id = zone.Id }, ApiResponse<ZoneResponse>.Ok(zone));
    }
}
