using FarmMonitoring.Api.Authorization;
using FarmMonitoring.Api.Contracts;
using FarmMonitoring.Application.Features.Farms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmMonitoring.Api.Controllers;

[ApiController]
[Route("api/zones")]
[Authorize(Policy = AccessPolicies.ReadFarmData)]
[Produces("application/json")]
[ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ApiError>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
public sealed class ZonesController(FarmService farms) : ControllerBase
{
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<ZoneResponse>>> Get(int id, CancellationToken ct) =>
        Ok(ApiResponse<ZoneResponse>.Ok(await farms.GetZoneAsync(id, ct)));

    [HttpPut("{id:int}")]
    [Authorize(Policy = AccessPolicies.ManageFarms)]
    public async Task<ActionResult<ApiResponse<ZoneResponse>>> Update(int id, ZoneRequest request, CancellationToken ct) =>
        Ok(ApiResponse<ZoneResponse>.Ok(await farms.UpdateZoneAsync(id, request, ct)));
}
