using FarmMonitoring.Api.Authorization;
using FarmMonitoring.Api.Contracts;
using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.TechnicalSupport;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmMonitoring.Api.Controllers;
[ApiController]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/service-jobs/{jobId:int}")]
[Authorize(Policy = AccessPolicies.OperationalUser)]
public sealed class ServiceReportsController(ServiceReportService service) : ControllerBase
{
    [HttpGet("reports")]
    public async Task<ActionResult<ApiPageResponse<ServiceReportSummary>>> List(int jobId, [FromQuery] PageQuery query, CancellationToken ct) => Ok(ApiPageResponse<ServiceReportSummary>.From(await service.ListAsync(jobId, query, ct)));
    [HttpGet("reports/{id:int}")]
    public async Task<ActionResult<ApiResponse<ServiceReportResponse>>> Get(int jobId, int id, CancellationToken ct) => Ok(ApiResponse<ServiceReportResponse>.Ok(await service.GetAsync(jobId, id, ct)));
    [HttpPost("reports")]
    [RequestSizeLimit(62914560)]
    public async Task<ActionResult<ApiResponse<ServiceReportResponse>>> Submit(int jobId, ServiceReportRequest request, CancellationToken ct)
    {
        var row = await service.SubmitAsync(jobId, request, ct);
        return CreatedAtAction(nameof(Get), new { jobId, id = row.Id }, ApiResponse<ServiceReportResponse>.Ok(row));
    }
    [HttpPost("confirm-operation")]
    public async Task<ActionResult<ApiResponse<ServiceJobDetail>>> Confirm(int jobId, ServiceReportDecisionRequest request, CancellationToken ct) => Ok(ApiResponse<ServiceJobDetail>.Ok(await service.DecideAsync(jobId, "confirm-operation", request, ct)));
    [HttpPost("approve")]
    public async Task<ActionResult<ApiResponse<ServiceJobDetail>>> Approve(int jobId, ServiceReportDecisionRequest request, CancellationToken ct) => Ok(ApiResponse<ServiceJobDetail>.Ok(await service.DecideAsync(jobId, "approve", request, ct)));
    [HttpPost("return")]
    public async Task<ActionResult<ApiResponse<ServiceJobDetail>>> Return(int jobId, ServiceReportDecisionRequest request, CancellationToken ct) => Ok(ApiResponse<ServiceJobDetail>.Ok(await service.DecideAsync(jobId, "return", request, ct)));
}
