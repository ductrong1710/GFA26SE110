using FarmMonitoring.Api.Authorization;
using FarmMonitoring.Api.Contracts;
using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.CropSupport;
using FarmMonitoring.Application.Features.CropSeasons;
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
    [EndpointSummary("Read report-scoped sensor evidence after assignment")]
    [EndpointDescription("Admin, current reporter/member or assigned Engineer only. Optional from/to require explicit offsets, both endpoints, and at most 31 days; default observedAt -7/+1 days, or all-time for legacy reports. Pagination defaults 1/20, maximum 100. Invalid readings remain flagged.")]
    public async Task<ActionResult<ApiPageResponse<CropSensorReading>>> Readings(int id, [FromQuery] CropEvidenceQuery query, CancellationToken ct)
        => Ok(ApiPageResponse<CropSensorReading>.From(await service.ReadingsAsync(id, query, ct)));
    [HttpGet("{id:int}/care-activities")]
    [EndpointSummary("Read the linked season's journal through report permissions")]
    [EndpointDescription("Admin, current reporter/member or assigned Engineer only. Same season owner/farm/zone required. Date filter uses UTC days; from/to maximum 31 days, default observedAt -7/+1 days or legacy all-time. No season returns an empty page.")]
    public async Task<ActionResult<ApiPageResponse<ActivitySummary>>> Activities(int id, [FromQuery] CropEvidenceQuery query, CancellationToken ct)
        => Ok(ApiPageResponse<ActivitySummary>.From(await service.ActivitiesAsync(id, query, ct)));
    [HttpGet("{id:int}/care-activities/{activityId:int}/images")]
    [EndpointDescription("Report evidence permission required. Images must belong to an activity in the linked season; no general season access is granted. Out-of-scope activity returns 404.")]
    public async Task<ActionResult<ApiResponse<EvidenceImageData[]>>> ActivityImages(int id, int activityId, CancellationToken ct)
        => Ok(ApiResponse<EvidenceImageData[]>.Ok(await service.ActivityImagesAsync(id, activityId, ct)));
    [HttpGet("{id:int}/alerts")]
    [EndpointDescription("Report evidence permission required. Paged attached-channel/node alerts still in report farm/zone; same from/to observation window as sensor-readings. No general alert access is granted.")]
    public async Task<ActionResult<ApiPageResponse<CropSupportAlert>>> Alerts(int id, [FromQuery] CropEvidenceQuery query, CancellationToken ct)
        => Ok(ApiPageResponse<CropSupportAlert>.From(await service.AlertsAsync(id, query, ct)));
    [HttpGet("{id:int}/history")]
    [EndpointDescription("Report evidence permission required. Preserved actor/time/evidence events, paged newest first. Includes optional feedback, activity, assignment and referral references.")]
    public async Task<ActionResult<ApiPageResponse<CropConsultationDetail>>> History(int id, [FromQuery] PageQuery query, CancellationToken ct)
        => Ok(ApiPageResponse<CropConsultationDetail>.From(await service.HistoryAsync(id, query, ct)));
    [HttpPost("{id:int}/assign")]
    [EndpointSummary("Admin assigns an unclaimed NEW report to an active FarmEngineer")]
    [EndpointDescription("Requires expectedRevision. Atomically enters IN_REVIEW, records assigning/assigned users and notifies both parties. Concurrent assign/claim has one winner; stale/state conflict returns 409.")]
    public async Task<ActionResult<ApiResponse<CropConditionDetail>>> Assign(int id, AssignCropReportRequest request, CancellationToken ct)
        => Ok(ApiResponse<CropConditionDetail>.Ok(await service.AssignAsync(id, request, ct)));
    [HttpPost("{id:int}/follow-ups")]
    [EndpointSummary("Reporter records feedback or requests review of a closed case")]
    [EndpointDescription("Current FarmOwner reporter/membership required. Types: ACKNOWLEDGED, AGREED, QUESTION, ACTION_TAKEN, IMPROVED, NOT_IMPROVED. Content and expectedRevision required; images and same-linked-season cultivationActivityId optional. After recommendation, preserves current state. RESOLVED (including legacy cases) accepts only NOT_IMPROVED, records REVIEW_REQUESTED and notifies Engineer without reopening. Completed seasons are allowed.")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<CropConditionDetail>>> FollowUp(int id, CropFollowUpRequest request, CancellationToken ct)
        => Ok(ApiResponse<CropConditionDetail>.Ok(await service.FollowUpAsync(id, request, ct)));
    [HttpPost("{id:int}/referrals")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    [EndpointDescription("Assigned Engineer requests DEVICE_INCIDENT or UAV_VIDEO in IN_REVIEW/AWAITING_INFORMATION. Reporter may link a permitted same-farm incident after a DEVICE_INCIDENT referral; identifiable sensor must match zone/channels. UAV_VIDEO has no ID and is pending MF-02 integration. Requires expectedRevision/content. Creates no service job, mission, payment or extra permissions.")]
    public async Task<ActionResult<ApiResponse<CropConditionDetail>>> Refer(int id, CropReferralRequest request, CancellationToken ct)
        => Ok(ApiResponse<CropConditionDetail>.Ok(await service.ReferAsync(id, request, ct)));
    [HttpPost("{id:int}/reopen")]
    [EndpointSummary("Assigned Engineer reopens RESOLVED with a reason")]
    [EndpointDescription("Content/reason and expectedRevision required. Appends REOPENED, enters IN_REVIEW, notifies reporter and preserves all history. Never updates the linked season.")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<CropConditionDetail>>> Reopen(int id, CropConsultationRequest request, CancellationToken ct)
        => Ok(ApiResponse<CropConditionDetail>.Ok(await service.UpdateAsync(id, CropConsultationAction.REOPENED, request, ct)));
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
    [EndpointDescription("Current FarmOwner/member creates a report. Optional cropSeasonId must be owned with exact matching farm/zone/crop; crop is snapshotted. Optional observedAt must contain Z/offset and is stored UTC; omitted legacy values remain null. reportedAt is server time. See API.md for bounded evidence and action contracts.")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<CropConditionDetail>>> Create(CreateCropConditionRequest request, CancellationToken ct)
    {
        var row = await service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = row.Id }, ApiResponse<CropConditionDetail>.Ok(row));
    }
}
