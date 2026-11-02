using FarmMonitoring.Api.Authorization;
using FarmMonitoring.Api.Contracts;
using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Installations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmMonitoring.Api.Controllers;

[ApiController]
[Route("api/installation-jobs")]
[Authorize(Policy = AccessPolicies.OperationalUser)]
public sealed class InstallationJobsController(InstallationJobService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiPageResponse<JobSummary>>> List([FromQuery] PageQuery query, CancellationToken ct) => Ok(ApiPageResponse<JobSummary>.From(await service.ListAsync(query, ct)));
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<JobDetail>>> Get(int id, CancellationToken ct) => Ok(ApiResponse<JobDetail>.Ok(await service.GetAsync(id, ct)));
    [HttpGet("technicians")]
    [Authorize(Policy = AccessPolicies.ReviewInstallations)]
    public async Task<ActionResult<ApiPageResponse<TechnicianOption>>> Technicians([FromQuery] PageQuery query, CancellationToken ct) => Ok(ApiPageResponse<TechnicianOption>.From(await service.TechniciansAsync(query, ct)));
    [HttpPost("/api/installation-requests/{id:int}/jobs")]
    [Authorize(Policy = AccessPolicies.ReviewInstallations)]
    public async Task<ActionResult<ApiResponse<JobDetail>>> Create(int id, JobAssignmentRequest request, CancellationToken ct)
    {
        var row = await service.AssignAsync(id, true, request, ct);
        return CreatedAtAction(nameof(Get), new { id = row.Id }, ApiResponse<JobDetail>.Ok(row));
    }
    [HttpPost("{id:int}/assignment")]
    [Authorize(Policy = AccessPolicies.ReviewInstallations)]
    public async Task<ActionResult<ApiResponse<JobDetail>>> Assign(int id, JobAssignmentRequest request, CancellationToken ct) => Ok(ApiResponse<JobDetail>.Ok(await service.AssignAsync(id, false, request, ct)));
    [HttpPost("{id:int}/start")]
    public async Task<ActionResult<ApiResponse<JobDetail>>> Start(int id, JobStartRequest request, CancellationToken ct) => Ok(ApiResponse<JobDetail>.Ok(await service.StartAsync(id, request, ct)));
}
