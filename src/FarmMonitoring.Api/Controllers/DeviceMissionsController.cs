using System.Globalization;
using FarmMonitoring.Api.Authorization;
using FarmMonitoring.Api.Contracts;
using FarmMonitoring.Application.Features.Missions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmMonitoring.Api.Controllers;

[ApiController]
[Route("api/device/gateways/{gatewayId:int}/missions")]
[Authorize(AuthenticationSchemes = DeviceAuthenticationHandler.SchemeName)]
[Produces("application/json")]
[ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ApiError>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
[ProducesResponseType<ApiError>(StatusCodes.Status422UnprocessableEntity)]
public sealed class DeviceMissionsController(DeviceMissionService missions, MissionLaunchService launch) : ControllerBase
{
    [HttpPost("{missionId:int}/launch/ready")]
    public async Task<ActionResult<ApiResponse<MissionLaunchResponse>>> Ready(int gatewayId, int missionId, MissionLaunchRequest request, CancellationToken ct) =>
        Ok(ApiResponse<MissionLaunchResponse>.Ok(await launch.DeviceAsync(missionId, gatewayId, Gateway, request, false, ct)));
    [HttpPost("{missionId:int}/launch/consume")]
    public async Task<ActionResult<ApiResponse<MissionLaunchResponse>>> Consume(int gatewayId, int missionId, MissionLaunchRequest request, CancellationToken ct) =>
        Ok(ApiResponse<MissionLaunchResponse>.Ok(await launch.DeviceAsync(missionId, gatewayId, Gateway, request, true, ct)));
    private int Gateway => int.Parse(User.FindFirst("gateway_id")!.Value, CultureInfo.InvariantCulture);
    [HttpGet("next")]
    public async Task<ActionResult<ApiResponse<DeviceMissionResponse?>>> Next(int gatewayId, CancellationToken ct) =>
        Ok(ApiResponse<DeviceMissionResponse?>.Ok(await missions.GetNextForGatewayAsync(gatewayId, Gateway, ct)));
    [HttpPost("{missionId:int}/downloaded")]
    public async Task<ActionResult<ApiResponse<DeviceMissionStateResponse>>> Downloaded(int gatewayId, int missionId, CancellationToken ct) =>
        Ok(ApiResponse<DeviceMissionStateResponse>.Ok(await missions.MarkDownloadedAsync(gatewayId, Gateway, missionId, ct)));
    [HttpPost("{missionId:int}/start")]
    public async Task<ActionResult<ApiResponse<DeviceMissionStateResponse>>> Start(int gatewayId, int missionId, CancellationToken ct) =>
        Ok(ApiResponse<DeviceMissionStateResponse>.Ok(await missions.StartAsync(gatewayId, Gateway, missionId, ct)));
    [HttpPost("{missionId:int}/complete")]
    public async Task<ActionResult<ApiResponse<DeviceMissionStateResponse>>> Complete(int gatewayId, int missionId, CancellationToken ct) =>
        Ok(ApiResponse<DeviceMissionStateResponse>.Ok(await missions.CompleteAsync(gatewayId, Gateway, missionId, ct)));
    [HttpPost("{missionId:int}/fail")]
    public async Task<ActionResult<ApiResponse<DeviceMissionStateResponse>>> Fail(int gatewayId, int missionId, DeviceMissionFailureRequest request, CancellationToken ct) =>
        Ok(ApiResponse<DeviceMissionStateResponse>.Ok(await missions.FailAsync(gatewayId, Gateway, missionId, request, ct)));
    [HttpPost("{missionId:int}/cancel")]
    public async Task<ActionResult<ApiResponse<DeviceMissionStateResponse>>> Cancel(int gatewayId, int missionId, DeviceMissionCancelRequest request, CancellationToken ct) =>
        Ok(ApiResponse<DeviceMissionStateResponse>.Ok(await missions.CancelAsync(gatewayId, Gateway, missionId, request, ct)));
}
