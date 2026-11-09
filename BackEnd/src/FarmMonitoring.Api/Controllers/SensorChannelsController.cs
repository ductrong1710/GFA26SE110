using FarmMonitoring.Api.Authorization;
using FarmMonitoring.Api.Contracts;
using FarmMonitoring.Application.Features.Sensors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmMonitoring.Api.Controllers;

[ApiController]
[Route("api/sensor-channels")]
[Authorize(Policy = AccessPolicies.ManageDevices)]
[Produces("application/json")]
[ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ApiError>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
public sealed class SensorChannelsController(SensorService sensors) : ControllerBase
{
    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse<SensorChannelResponse>>> Update(int id, SensorChannelRequest request, CancellationToken ct) =>
        Ok(ApiResponse<SensorChannelResponse>.Ok(await sensors.UpdateChannelAsync(id, request, ct)));
}
