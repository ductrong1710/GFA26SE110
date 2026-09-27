using FarmMonitoring.Api.Authorization;
using FarmMonitoring.Api.Contracts;
using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Equipment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmMonitoring.Api.Controllers;

[ApiController]
[Route("api/gateways")]
[Authorize(Policy = AccessPolicies.ReadFarmData)]
[Produces("application/json")]
[ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ApiError>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
public sealed class GatewaysController(EquipmentService equipment) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiPageResponse<GatewayResponse>>> List([FromQuery] PageQuery query, CancellationToken ct) =>
        Ok(ApiPageResponse<GatewayResponse>.From(await equipment.ListGatewaysAsync(query, ct)));
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<GatewayResponse>>> Get(int id, CancellationToken ct) =>
        Ok(ApiResponse<GatewayResponse>.Ok(await equipment.GetGatewayAsync(id, ct)));
    [HttpPost]
    [Authorize(Policy = AccessPolicies.ManageDevices)]
    [ProducesResponseType<ApiResponse<GatewayResponse>>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<GatewayResponse>>> Create(GatewayRequest request, CancellationToken ct)
    {
        var gateway = await equipment.CreateGatewayAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = gateway.Id }, ApiResponse<GatewayResponse>.Ok(gateway));
    }
    [HttpPut("{id:int}")]
    [Authorize(Policy = AccessPolicies.ManageDevices)]
    public async Task<ActionResult<ApiResponse<GatewayResponse>>> Update(int id, GatewayRequest request, CancellationToken ct) =>
        Ok(ApiResponse<GatewayResponse>.Ok(await equipment.UpdateGatewayAsync(id, request, ct)));
    [HttpPatch("{id:int}/status")]
    [Authorize(Policy = AccessPolicies.ManageDevices)]
    public async Task<ActionResult<ApiResponse<GatewayResponse>>> Status(int id, EquipmentStatusRequest request, CancellationToken ct) =>
        Ok(ApiResponse<GatewayResponse>.Ok(await equipment.SetGatewayStatusAsync(id, request, ct)));
    [HttpPost("{id:int}/assign-uav")]
    [Authorize(Policy = AccessPolicies.ManageDevices)]
    public async Task<ActionResult<ApiResponse<GatewayResponse>>> Assign(int id, AssignUavRequest request, CancellationToken ct) =>
        Ok(ApiResponse<GatewayResponse>.Ok(await equipment.AssignUavAsync(id, request, ct)));
}
