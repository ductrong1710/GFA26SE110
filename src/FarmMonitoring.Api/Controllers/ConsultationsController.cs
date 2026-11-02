using FarmMonitoring.Api.Authorization;
using FarmMonitoring.Api.Contracts;
using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Consultations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmMonitoring.Api.Controllers;

[ApiController]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/consultations")]
[Authorize]
public sealed class ConsultationsController(ConsultationService service) : ControllerBase
{
    [HttpPost]
    [AllowAnonymous]
    [RequestSizeLimit(32768)]
    public async Task<ActionResult<ApiResponse<ConsultationReceipt>>> Submit(SubmitConsultationRequest request, CancellationToken ct)
    {
        var receipt = await service.SubmitAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<ConsultationReceipt>.Ok(receipt));
    }

    [HttpGet]
    [Authorize(Policy = AccessPolicies.ReadConsultations)]
    public async Task<ActionResult<ApiPageResponse<ConsultationSummary>>> List([FromQuery] ConsultationQuery query, CancellationToken ct) =>
        Ok(ApiPageResponse<ConsultationSummary>.From(await service.ListAsync(query, ct)));

    [HttpGet("{id:int}")]
    [Authorize(Policy = AccessPolicies.ReadConsultations)]
    public async Task<ActionResult<ApiResponse<ConsultationResponse>>> Get(int id, CancellationToken ct) =>
        Ok(ApiResponse<ConsultationResponse>.Ok(await service.GetAsync(id, ct)));

    [HttpPost("{id:int}/claim")]
    [Authorize(Policy = AccessPolicies.ProcessConsultations)]
    public async Task<ActionResult<ApiResponse<ConsultationResponse>>> Claim(int id, CancellationToken ct) =>
        Ok(ApiResponse<ConsultationResponse>.Ok(await service.ClaimAsync(id, ct)));

    [HttpPost("{id:int}/follow-ups")]
    [Authorize(Policy = AccessPolicies.ProcessConsultations)]
    public async Task<ActionResult<ApiResponse<ConsultationResponse>>> FollowUp(int id, FollowUpRequest request, CancellationToken ct) =>
        Ok(ApiResponse<ConsultationResponse>.Ok(await service.FollowUpAsync(id, request, ct)));

    [HttpPost("{id:int}/surveys")]
    [Authorize(Policy = AccessPolicies.ProcessConsultations)]
    [RequestSizeLimit(20971520)]
    public async Task<ActionResult<ApiResponse<SurveyResponse>>> RecordSurvey(int id, SurveyRequest request, CancellationToken ct)
    {
        var survey = await service.RecordSurveyAsync(id, request, ct);
        return CreatedAtAction(nameof(Survey), new { id, surveyId = survey.Id }, ApiResponse<SurveyResponse>.Ok(survey));
    }

    [HttpGet("{id:int}/surveys")]
    [Authorize(Policy = AccessPolicies.OperationalUser)]
    public async Task<ActionResult<ApiPageResponse<SurveySummary>>> Surveys(int id, [FromQuery] PageQuery query, CancellationToken ct) =>
        Ok(ApiPageResponse<SurveySummary>.From(await service.ListSurveysAsync(id, query, ct)));

    [HttpGet("{id:int}/surveys/{surveyId:int}")]
    [Authorize(Policy = AccessPolicies.OperationalUser)]
    public async Task<ActionResult<ApiResponse<SurveyResponse>>> Survey(int id, int surveyId, CancellationToken ct) =>
        Ok(ApiResponse<SurveyResponse>.Ok(await service.GetSurveyAsync(id, surveyId, ct)));

    [HttpPut("{id:int}/customer-farm")]
    [Authorize(Policy = AccessPolicies.ManageUsers)]
    public async Task<ActionResult<ApiResponse<ConsultationResponse>>> BindCustomer(int id, CustomerFarmRequest request, CancellationToken ct) =>
        Ok(ApiResponse<ConsultationResponse>.Ok(await service.BindCustomerAsync(id, request, ct)));

    [HttpGet("/api/farms/{farmId:int}/survey-consultations")]
    [Authorize(Policy = AccessPolicies.ReadFarmData)]
    public async Task<ActionResult<ApiPageResponse<ConsultationSummary>>> FarmConsultations(int farmId, [FromQuery] PageQuery query, CancellationToken ct) =>
        Ok(ApiPageResponse<ConsultationSummary>.From(await service.FarmConsultationsAsync(farmId, query, ct)));
}
