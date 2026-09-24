using FarmMonitoring.Api.Authorization;
using FarmMonitoring.Api.Contracts;
using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Sensors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmMonitoring.Api.Controllers;

[ApiController]
[Route("api/sensor-nodes")]
[Authorize(Policy = AccessPolicies.ReadFarmData)]
[Produces("application/json")]
[ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ApiError>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
public sealed class SensorNodesController(SensorService sensors) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiPageResponse<SensorNodeResponse>>> List([FromQuery] SensorNodeQuery query, CancellationToken ct) =>
        Ok(ApiPageResponse<SensorNodeResponse>.From(await sensors.ListNodesAsync(query, ct)));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<SensorNodeResponse>>> Get(int id, CancellationToken ct) =>
        Ok(ApiResponse<SensorNodeResponse>.Ok(await sensors.GetNodeAsync(id, ct)));

    [HttpPost]
    [Authorize(Policy = AccessPolicies.ManageDevices)]
    [ProducesResponseType<ApiResponse<SensorNodeResponse>>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<SensorNodeResponse>>> Create(SensorNodeRequest request, CancellationToken ct)
    {
        var node = await sensors.CreateNodeAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = node.Id }, ApiResponse<SensorNodeResponse>.Ok(node));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = AccessPolicies.ManageDevices)]
    public async Task<ActionResult<ApiResponse<SensorNodeResponse>>> Update(int id, SensorNodeRequest request, CancellationToken ct) =>
        Ok(ApiResponse<SensorNodeResponse>.Ok(await sensors.UpdateNodeAsync(id, request, ct)));

    [HttpPatch("{id:int}/status")]
    [Authorize(Policy = AccessPolicies.ManageDevices)]
    public async Task<ActionResult<ApiResponse<SensorNodeResponse>>> Status(int id, SensorNodeStatusRequest request, CancellationToken ct) =>
        Ok(ApiResponse<SensorNodeResponse>.Ok(await sensors.SetStatusAsync(id, request, ct)));

    [HttpGet("{sensorNodeId:int}/channels")]
    public async Task<ActionResult<ApiPageResponse<SensorChannelResponse>>> Channels(int sensorNodeId, [FromQuery] PageQuery query, CancellationToken ct) =>
        Ok(ApiPageResponse<SensorChannelResponse>.From(await sensors.ListChannelsAsync(sensorNodeId, query, ct)));

    [HttpPost("{sensorNodeId:int}/channels")]
    [Authorize(Policy = AccessPolicies.ManageDevices)]
    [ProducesResponseType<ApiResponse<SensorChannelResponse>>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<SensorChannelResponse>>> CreateChannel(int sensorNodeId, SensorChannelRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, ApiResponse<SensorChannelResponse>.Ok(await sensors.CreateChannelAsync(sensorNodeId, request, ct)));
}
