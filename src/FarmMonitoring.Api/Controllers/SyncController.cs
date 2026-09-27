using System.Globalization;
using FarmMonitoring.Api.Authorization;
using FarmMonitoring.Api.Contracts;
using FarmMonitoring.Application.Features.Sync;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmMonitoring.Api.Controllers;

[ApiController]
[Route("api/device/gateways/{gatewayId:int}/sync")]
[Authorize(AuthenticationSchemes = DeviceAuthenticationHandler.SchemeName)]
[Produces("application/json")]
[ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ApiError>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
[ProducesResponseType<ApiError>(StatusCodes.Status422UnprocessableEntity)]
public sealed class SyncController(SyncService sync) : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(2_000_000)]
    public async Task<ActionResult<ApiResponse<SyncResponse>>> Synchronize(int gatewayId, SyncRequest request, CancellationToken ct) =>
        Ok(ApiResponse<SyncResponse>.Ok(await sync.SynchronizeAsync(gatewayId,
            int.Parse(User.FindFirst("gateway_id")!.Value, CultureInfo.InvariantCulture), request, ct)));
}
