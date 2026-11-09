using System.Globalization;
using FarmMonitoring.Api.Authorization;
using FarmMonitoring.Api.Contracts;
using FarmMonitoring.Application.Features.Equipment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmMonitoring.Api.Controllers;

[ApiController]
[Route("api/device/gateways/{gatewayId:int}/heartbeat")]
[Authorize(AuthenticationSchemes = DeviceAuthenticationHandler.SchemeName)]
[Produces("application/json")]
public sealed class GatewayHeartbeatController(EquipmentService equipment) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<ApiResponse<GatewayHeartbeatResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<GatewayHeartbeatResponse>>> Post(int gatewayId, CancellationToken ct) =>
        Ok(ApiResponse<GatewayHeartbeatResponse>.Ok(await equipment.HeartbeatAsync(gatewayId,
            int.Parse(User.FindFirst("gateway_id")!.Value, CultureInfo.InvariantCulture), ct)));
}
