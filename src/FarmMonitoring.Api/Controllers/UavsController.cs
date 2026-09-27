using FarmMonitoring.Api.Authorization;
using FarmMonitoring.Api.Contracts;
using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Equipment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmMonitoring.Api.Controllers;

[ApiController]
[Route("api/uavs")]
[Authorize(Policy = AccessPolicies.ReadFarmData)]
[Produces("application/json")]
[ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ApiError>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
public sealed class UavsController(EquipmentService equipment) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiPageResponse<UavResponse>>> List([FromQuery] PageQuery query, CancellationToken ct) =>
        Ok(ApiPageResponse<UavResponse>.From(await equipment.ListUavsAsync(query, ct)));
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<UavResponse>>> Get(int id, CancellationToken ct) =>
        Ok(ApiResponse<UavResponse>.Ok(await equipment.GetUavAsync(id, ct)));
    [HttpPost]
    [Authorize(Policy = AccessPolicies.ManageDevices)]
    [ProducesResponseType<ApiResponse<UavResponse>>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<UavResponse>>> Create(UavRequest request, CancellationToken ct)
    {
        var uav = await equipment.CreateUavAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = uav.Id }, ApiResponse<UavResponse>.Ok(uav));
    }
    [HttpPut("{id:int}")]
    [Authorize(Policy = AccessPolicies.ManageDevices)]
    public async Task<ActionResult<ApiResponse<UavResponse>>> Update(int id, UavRequest request, CancellationToken ct) =>
        Ok(ApiResponse<UavResponse>.Ok(await equipment.UpdateUavAsync(id, request, ct)));
    [HttpPatch("{id:int}/status")]
    [Authorize(Policy = AccessPolicies.ManageDevices)]
    public async Task<ActionResult<ApiResponse<UavResponse>>> Status(int id, EquipmentStatusRequest request, CancellationToken ct) =>
        Ok(ApiResponse<UavResponse>.Ok(await equipment.SetUavStatusAsync(id, request, ct)));
}
