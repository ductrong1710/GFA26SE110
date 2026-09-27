using System.Globalization;
using FarmMonitoring.Api.Authorization;
using FarmMonitoring.Api.Contracts;
using FarmMonitoring.Application.Features.Telemetry;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmMonitoring.Api.Controllers;

[ApiController]
[Produces("application/json")]
[ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ApiError>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ApiError>(StatusCodes.Status422UnprocessableEntity)]
public sealed class TelemetryController(TelemetryService telemetry) : ControllerBase
{
    [HttpPost("api/device/telemetry")]
    [Authorize(AuthenticationSchemes = DeviceAuthenticationHandler.SchemeName)]
    [ProducesResponseType<ApiResponse<TelemetryResponse>>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<TelemetryResponse>>> Record(TelemetryRequest request, CancellationToken ct)
    {
        var gateway = int.Parse(User.FindFirst("gateway_id")!.Value, CultureInfo.InvariantCulture);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<TelemetryResponse>.Ok(await telemetry.RecordAsync(gateway, request, ct)));
    }
    [HttpGet("api/missions/{id:int}/telemetry/latest")]
    [Authorize(Policy = AccessPolicies.ReadFarmData)]
    public async Task<ActionResult<ApiResponse<TelemetryResponse>>> Latest(int id, CancellationToken ct) =>
        Ok(ApiResponse<TelemetryResponse>.Ok(await telemetry.LatestAsync(id, ct)));
    [HttpGet("api/missions/{id:int}/telemetry")]
    [Authorize(Policy = AccessPolicies.ReadFarmData)]
    public async Task<ActionResult<ApiPageResponse<TelemetryResponse>>> History(int id, [FromQuery] TelemetryQuery query, CancellationToken ct) =>
        Ok(ApiPageResponse<TelemetryResponse>.From(await telemetry.ListAsync(id, query, ct)));
}
