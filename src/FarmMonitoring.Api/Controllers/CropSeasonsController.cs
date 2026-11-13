using FarmMonitoring.Api.Authorization;
using FarmMonitoring.Api.Contracts;
using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.CropSeasons;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmMonitoring.Api.Controllers;

[ApiController]
[Route("api/crop-seasons")]
[Authorize(Policy = AccessPolicies.ReadFarmData)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CropSeasonsController(CropSeasonService service) : ControllerBase
{
    [HttpGet("compare")]
    public async Task<ActionResult<ApiResponse<SeasonComparison[]>>> Compare([FromQuery] int[] ids, CancellationToken ct)
        => Ok(ApiResponse<SeasonComparison[]>.Ok(await service.CompareAsync(ids, ct)));

    [HttpPost("{id:int}/activities")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<CropSeasonDetail>>> RecordActivity(int id, CultivationActivityRequest request, CancellationToken ct)
        => Ok(ApiResponse<CropSeasonDetail>.Ok(await service.RecordActivityAsync(id, request, ct)));

    [HttpGet("{id:int}/activities")]
    public async Task<ActionResult<ApiPageResponse<ActivitySummary>>> Activities(int id, [FromQuery] PageQuery query, CancellationToken ct)
        => Ok(ApiPageResponse<ActivitySummary>.From(await service.ActivitiesAsync(id, query, ct)));

    [HttpGet("{id:int}/activities/{activityId:int}/images")]
    public async Task<ActionResult<ApiResponse<EvidenceImageData[]>>> Images(int id, int activityId, CancellationToken ct)
        => Ok(ApiResponse<EvidenceImageData[]>.Ok(await service.ImagesAsync(id, activityId, ct)));

    [HttpPost("{id:int}/complete")]
    public async Task<ActionResult<ApiResponse<CropSeasonDetail>>> Complete(int id, CompleteCropSeasonRequest request, CancellationToken ct)
        => Ok(ApiResponse<CropSeasonDetail>.Ok(await service.CompleteAsync(id, request, ct)));

    [HttpGet]
    public async Task<ActionResult<ApiPageResponse<CropSeasonDetail>>> List([FromQuery] CropSeasonQuery query, CancellationToken ct)
        => Ok(ApiPageResponse<CropSeasonDetail>.From(await service.ListAsync(query, ct)));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<CropSeasonDetail>>> Get(int id, CancellationToken ct)
        => Ok(ApiResponse<CropSeasonDetail>.Ok(await service.GetAsync(id, ct)));

    [HttpPost]
    public async Task<ActionResult<ApiResponse<CropSeasonDetail>>> Create(CropSeasonRequest request, CancellationToken ct)
    {
        var row = await service.SaveAsync(null, request, ct);
        return CreatedAtAction(nameof(Get), new { id = row.Id }, ApiResponse<CropSeasonDetail>.Ok(row));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse<CropSeasonDetail>>> Update(int id, CropSeasonRequest request, CancellationToken ct)
        => Ok(ApiResponse<CropSeasonDetail>.Ok(await service.SaveAsync(id, request, ct)));
}
