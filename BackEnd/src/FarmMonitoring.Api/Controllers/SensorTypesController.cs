using FarmMonitoring.Api.Authorization;
using FarmMonitoring.Api.Contracts;
using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Sensors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmMonitoring.Api.Controllers;

[ApiController]
[Route("api/sensor-types")]
[Authorize(Policy = AccessPolicies.ReadFarmData)]
[Produces("application/json")]
[ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ApiError>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
public sealed class SensorTypesController(SensorService sensors) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiPageResponse<SensorTypeResponse>>> List([FromQuery] PageQuery query, CancellationToken ct) =>
        Ok(ApiPageResponse<SensorTypeResponse>.From(await sensors.ListTypesAsync(query, ct)));

    [HttpPost]
    [Authorize(Policy = AccessPolicies.ManageDevices)]
    [ProducesResponseType<ApiResponse<SensorTypeResponse>>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<SensorTypeResponse>>> Create(SensorTypeRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, ApiResponse<SensorTypeResponse>.Ok(await sensors.CreateTypeAsync(request, ct)));
}
