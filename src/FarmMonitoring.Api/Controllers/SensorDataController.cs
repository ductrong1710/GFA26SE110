using FarmMonitoring.Api.Authorization;
using FarmMonitoring.Api.Contracts;
using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.SensorData;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmMonitoring.Api.Controllers;

[ApiController]
[Authorize(Policy = AccessPolicies.ReadFarmData)]
[Produces("application/json")]
[ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ApiError>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
public sealed class SensorDataController(SensorDataService data) : ControllerBase
{
    [HttpGet("api/sensor-readings")]
    public async Task<ActionResult<ApiPageResponse<ReadingResponse>>> List([FromQuery] ReadingQuery query, CancellationToken ct) =>
        Ok(ApiPageResponse<ReadingResponse>.From(await data.ListAsync(query, null, ct)));
    [HttpGet("api/sensor-channels/{id:int}/history")]
    public async Task<ActionResult<ApiPageResponse<ReadingResponse>>> History(int id, [FromQuery] ReadingQuery query, CancellationToken ct) =>
        Ok(ApiPageResponse<ReadingResponse>.From(await data.ListAsync(query, id, ct)));
    [HttpGet("api/sensor-nodes/{id:int}/latest")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<LatestChannelResponse>>>> Latest(int id, CancellationToken ct) =>
        Ok(ApiResponse<IReadOnlyList<LatestChannelResponse>>.Ok(await data.LatestAsync(id, ct)));
    [HttpGet("api/zones/compare")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ZoneComparisonResponse>>>> Compare([FromQuery] ZoneComparisonQuery query, CancellationToken ct) =>
        Ok(ApiResponse<IReadOnlyList<ZoneComparisonResponse>>.Ok(await data.CompareAsync(query, ct)));
    [HttpGet("api/missions/{missionId:int}/collection-attempts")]
    public async Task<ActionResult<ApiPageResponse<CollectionAttemptResponse>>> Attempts(int missionId, [FromQuery] PageQuery query, CancellationToken ct) =>
        Ok(ApiPageResponse<CollectionAttemptResponse>.From(await data.AttemptsAsync(missionId, query, ct)));
}
