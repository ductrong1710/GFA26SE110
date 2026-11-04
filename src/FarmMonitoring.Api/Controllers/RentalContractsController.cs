using FarmMonitoring.Api.Authorization;
using FarmMonitoring.Api.Contracts;
using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Rentals;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmMonitoring.Api.Controllers;
[ApiController]
[Route("api/rental-contracts")]
[Authorize(Policy = AccessPolicies.OperationalUser)]
public sealed class RentalContractsController(RentalService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiPageResponse<RentalSummary>>> List([FromQuery] PageQuery query, CancellationToken ct) => Ok(ApiPageResponse<RentalSummary>.From(await service.ListAsync(query, ct)));
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<RentalDetail>>> Get(int id, CancellationToken ct) => Ok(ApiResponse<RentalDetail>.Ok(await service.GetAsync(id, ct)));
    [HttpPost("/api/installation-jobs/{jobId:int}/rental-contract")]
    [Authorize(Policy = AccessPolicies.ReviewInstallations)]
    public async Task<ActionResult<ApiResponse<RentalDetail>>> Create(int jobId, CreateRentalRequest request, CancellationToken ct)
    {
        var row = await service.CreateAsync(jobId, request, ct);
        return CreatedAtAction(nameof(Get), new { id = row.Id }, ApiResponse<RentalDetail>.Ok(row));
    }
    [HttpPost("{id:int}/confirm-start")]
    [Authorize(Policy = AccessPolicies.ReviewInstallations)]
    public async Task<ActionResult<ApiResponse<RentalDetail>>> ConfirmStart(int id, ConfirmRentalStartRequest request, CancellationToken ct) =>
        Ok(ApiResponse<RentalDetail>.Ok(await service.ConfirmStartAsync(id, request, ct)));
    [HttpPost("{id:int}/activate")]
    [Authorize(Policy = AccessPolicies.ReviewInstallations)]
    public async Task<ActionResult<ApiResponse<RentalDetail>>> Activate(int id, RentalActionRequest request, CancellationToken ct) => Ok(ApiResponse<RentalDetail>.Ok(await service.ActivateAsync(id, request, ct)));
    [HttpPost("{id:int}/extend")]
    [Authorize(Policy = AccessPolicies.ReviewInstallations)]
    public async Task<ActionResult<ApiResponse<RentalDetail>>> Extend(int id, ExtendRentalRequest request, CancellationToken ct) => Ok(ApiResponse<RentalDetail>.Ok(await service.ExtendAsync(id, request, ct)));
    [HttpPost("{id:int}/end")]
    [Authorize(Policy = AccessPolicies.ReviewInstallations)]
    public async Task<ActionResult<ApiResponse<RentalDetail>>> End(int id, EndRentalRequest request, CancellationToken ct) => Ok(ApiResponse<RentalDetail>.Ok(await service.EndAsync(id, request, ct)));
}
