using FarmMonitoring.Api.Authorization;
using FarmMonitoring.Api.Contracts;
using FarmMonitoring.Application.Features.Thresholds;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmMonitoring.Api.Controllers;

[ApiController]
[Route("api/sensor-channels/{id:int}/threshold")]
[Authorize(Policy = AccessPolicies.ReadFarmData)]
[Produces("application/json")]
[ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ApiError>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
public sealed class ThresholdsController(ThresholdService thresholds) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<ThresholdResponse>>> Get(int id, CancellationToken ct) =>
        Ok(ApiResponse<ThresholdResponse>.Ok(await thresholds.GetAsync(id, ct)));
    [HttpPut]
    [Authorize(Policy = AccessPolicies.ConfigureThresholds)]
    public async Task<ActionResult<ApiResponse<ThresholdResponse>>> Set(int id, ThresholdRequest request, CancellationToken ct) =>
        Ok(ApiResponse<ThresholdResponse>.Ok(await thresholds.SetAsync(id, request, ct)));
}
