using FarmMonitoring.Api.Authorization;
using FarmMonitoring.Api.Contracts;
using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.CropSupport;
using FarmMonitoring.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmMonitoring.Api.Controllers;
[ApiController]
[Route("api/crop-condition-reports")]
[Authorize(Policy = AccessPolicies.OperationalUser)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CropConditionReportsController(CropSupportService service) : ControllerBase
{
    [HttpGet("{id:int}/crop-seasons")]
    public async Task<ActionResult<ApiPageResponse<SupportingCropSeason>>> Seasons(int id, [FromQuery] PageQuery query, CancellationToken ct)
        => Ok(ApiPageResponse<SupportingCropSeason>.From(await service.SeasonsAsync(id, query, ct)));

    [HttpGet("{id:int}/sensor-readings")]
    public async Task<ActionResult<ApiPageResponse<CropSensorReading>>> Readings(int id, [FromQuery] PageQuery query, CancellationToken ct)
        => Ok(ApiPageResponse<CropSensorReading>.From(await service.ReadingsAsync(id, query, ct)));
    [HttpPost("{id:int}/claim")]
    public async Task<ActionResult<ApiResponse<CropConditionDetail>>> Claim(int id, CancellationToken ct)
        => Ok(ApiResponse<CropConditionDetail>.Ok(await service.ClaimAsync(id, ct)));
    [HttpPost("{id:int}/request-information")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<CropConditionDetail>>> RequestInformation(int id, CropConsultationRequest request, CancellationToken ct)
        => Ok(ApiResponse<CropConditionDetail>.Ok(await service.UpdateAsync(id, CropConsultationAction.INFORMATION_REQUESTED, request, ct)));
    [HttpPost("{id:int}/reply")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<CropConditionDetail>>> Reply(int id, CropConsultationRequest request, CancellationToken ct)
        => Ok(ApiResponse<CropConditionDetail>.Ok(await service.UpdateAsync(id, CropConsultationAction.OWNER_REPLIED, request, ct)));
    [HttpPost("{id:int}/recommend")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<CropConditionDetail>>> Recommend(int id, CropConsultationRequest request, CancellationToken ct)
        => Ok(ApiResponse<CropConditionDetail>.Ok(await service.UpdateAsync(id, CropConsultationAction.RECOMMENDED, request, ct)));
    [HttpPost("{id:int}/resolve")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<CropConditionDetail>>> Resolve(int id, CropConsultationRequest request, CancellationToken ct)
        => Ok(ApiResponse<CropConditionDetail>.Ok(await service.UpdateAsync(id, CropConsultationAction.RESOLVED, request, ct)));
    [HttpGet]
    public async Task<ActionResult<ApiPageResponse<CropConditionSummary>>> List([FromQuery] PageQuery query, CancellationToken ct)
        => Ok(ApiPageResponse<CropConditionSummary>.From(await service.ListAsync(query, ct)));
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<CropConditionDetail>>> Get(int id, CancellationToken ct)
        => Ok(ApiResponse<CropConditionDetail>.Ok(await service.GetAsync(id, ct)));
    [HttpPost]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<CropConditionDetail>>> Create(CreateCropConditionRequest request, CancellationToken ct)
    {
        var row = await service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = row.Id }, ApiResponse<CropConditionDetail>.Ok(row));
    }
}
