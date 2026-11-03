using FarmMonitoring.Api.Authorization;
using FarmMonitoring.Api.Contracts;
using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Installations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmMonitoring.Api.Controllers;
[ApiController]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/installation-jobs/{jobId:int}")]
[Authorize(Policy = AccessPolicies.OperationalUser)]
public sealed class InstallationReportsController(InstallationReportService service) : ControllerBase
{
    [HttpGet("reports")]
    public async Task<ActionResult<ApiPageResponse<ReportSummary>>> List(int jobId, [FromQuery] PageQuery query, CancellationToken ct) => Ok(ApiPageResponse<ReportSummary>.From(await service.ListAsync(jobId, query, ct)));
    [HttpGet("reports/{id:int}")]
    public async Task<ActionResult<ApiResponse<InstallationReportResponse>>> Get(int jobId, int id, CancellationToken ct) => Ok(ApiResponse<InstallationReportResponse>.Ok(await service.GetAsync(jobId, id, ct)));
    [HttpPost("reports")]
    [RequestSizeLimit(62914560)]
    public async Task<ActionResult<ApiResponse<InstallationReportResponse>>> Submit(int jobId, InstallationReportRequest request, CancellationToken ct)
    {
        var row = await service.SubmitAsync(jobId, request, ct);
        return CreatedAtAction(nameof(Get), new { jobId, id = row.Id }, ApiResponse<InstallationReportResponse>.Ok(row));
    }
    [HttpPost("confirm-handover")]
    public async Task<ActionResult<ApiResponse<JobDetail>>> Confirm(int jobId, ReportDecisionRequest request, CancellationToken ct) => Ok(ApiResponse<JobDetail>.Ok(await service.DecideAsync(jobId, "confirm-handover", request, ct)));
    [HttpPost("approve")]
    [Authorize(Policy = AccessPolicies.ReviewInstallations)]
    public async Task<ActionResult<ApiResponse<JobDetail>>> Approve(int jobId, ReportDecisionRequest request, CancellationToken ct) => Ok(ApiResponse<JobDetail>.Ok(await service.DecideAsync(jobId, "approve", request, ct)));
    [HttpPost("return")]
    [Authorize(Policy = AccessPolicies.ReviewInstallations)]
    public async Task<ActionResult<ApiResponse<JobDetail>>> Return(int jobId, ReportDecisionRequest request, CancellationToken ct) => Ok(ApiResponse<JobDetail>.Ok(await service.DecideAsync(jobId, "return", request, ct)));
}
