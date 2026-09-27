using System.Globalization;
using FarmMonitoring.Api.Authorization;
using FarmMonitoring.Api.Contracts;
using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Missions;
using FarmMonitoring.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmMonitoring.Api.Controllers;

[ApiController]
[Route("api/missions")]
[Authorize(Policy = AccessPolicies.ReadFarmData)]
[Produces("application/json")]
[ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ApiError>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
[ProducesResponseType<ApiError>(StatusCodes.Status422UnprocessableEntity)]
public sealed class MissionsController(MissionService missions) : ControllerBase
{
    private int Actor => int.Parse(User.FindFirst("sub")!.Value, CultureInfo.InvariantCulture);
    [HttpGet]
    public async Task<ActionResult<ApiPageResponse<MissionSummary>>> List([FromQuery] MissionQuery query, CancellationToken ct) =>
        Ok(ApiPageResponse<MissionSummary>.From(await missions.ListAsync(query, ct)));
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<MissionResponse>>> Get(int id, CancellationToken ct) =>
        Ok(ApiResponse<MissionResponse>.Ok(await missions.GetAsync(id, ct)));
    [HttpPost]
    [Authorize(Policy = AccessPolicies.ManageMissions)]
    [ProducesResponseType<ApiResponse<MissionResponse>>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<MissionResponse>>> Create(MissionRequest request, CancellationToken ct)
    {
        var mission = await missions.CreateAsync(request, Actor, ct);
        return CreatedAtAction(nameof(Get), new { id = mission.Id }, ApiResponse<MissionResponse>.Ok(mission));
    }
    [HttpPut("{id:int}")]
    [Authorize(Policy = AccessPolicies.ManageMissions)]
    public async Task<ActionResult<ApiResponse<MissionResponse>>> Update(int id, MissionRequest request, CancellationToken ct) =>
        Ok(ApiResponse<MissionResponse>.Ok(await missions.UpdateAsync(id, request, Actor, ct)));
    [HttpPost("{id:int}/schedule")]
    [Authorize(Policy = AccessPolicies.ManageMissions)]
    public async Task<ActionResult<ApiResponse<MissionResponse>>> Schedule(int id, ScheduleMissionRequest request, CancellationToken ct) =>
        Ok(ApiResponse<MissionResponse>.Ok(await missions.ScheduleAsync(id, request, Actor, ct)));
    [HttpPost("{id:int}/start")]
    [Authorize(Policy = AccessPolicies.ManageMissions)]
    public Task<ActionResult<ApiResponse<MissionResponse>>> Start(int id, CancellationToken ct) =>
        Transition(id, MissionStatus.RUNNING, new(null, null, null, null), ct);
    [HttpPost("{id:int}/complete")]
    [Authorize(Policy = AccessPolicies.ManageMissions)]
    public Task<ActionResult<ApiResponse<MissionResponse>>> Complete(int id, MissionActionRequest request, CancellationToken ct) =>
        Transition(id, MissionStatus.COMPLETED, request, ct);
    [HttpPost("{id:int}/fail")]
    [Authorize(Policy = AccessPolicies.ManageMissions)]
    public Task<ActionResult<ApiResponse<MissionResponse>>> Fail(int id, MissionActionRequest request, CancellationToken ct) =>
        Transition(id, MissionStatus.FAILED, request, ct);
    [HttpPost("{id:int}/cancel")]
    [Authorize(Policy = AccessPolicies.ManageMissions)]
    public Task<ActionResult<ApiResponse<MissionResponse>>> Cancel(int id, MissionActionRequest request, CancellationToken ct) =>
        Transition(id, MissionStatus.CANCELLED, request, ct);
    [HttpPatch("{id:int}/status")]
    [Authorize(Policy = AccessPolicies.ManageMissions)]
    public async Task<ActionResult<ApiResponse<MissionResponse>>> Status(int id, MissionStatusRequest request, CancellationToken ct) =>
        Ok(ApiResponse<MissionResponse>.Ok(await missions.PatchStatusAsync(id, request, Actor, ct)));
    [HttpGet("{id:int}/results")]
    public async Task<ActionResult<ApiResponse<MissionResults>>> Results(int id, CancellationToken ct) =>
        Ok(ApiResponse<MissionResults>.Ok(await missions.ResultsAsync(id, ct)));
    [HttpGet("{id:int}/waypoints")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<WaypointResponse>>>> Waypoints(int id, CancellationToken ct) =>
        Ok(ApiResponse<IReadOnlyList<WaypointResponse>>.Ok(await missions.WaypointsAsync(id, ct)));
    [HttpGet("{id:int}/logs")]
    public async Task<ActionResult<ApiPageResponse<MissionLogResponse>>> Logs(int id, [FromQuery] PageQuery query, CancellationToken ct) =>
        Ok(ApiPageResponse<MissionLogResponse>.From(await missions.LogsAsync(id, query, ct)));
    private async Task<ActionResult<ApiResponse<MissionResponse>>> Transition(int id, MissionStatus status, MissionActionRequest request, CancellationToken ct) =>
        Ok(ApiResponse<MissionResponse>.Ok(await missions.ChangeStatusAsync(id, status, request, Actor, ct)));
}
