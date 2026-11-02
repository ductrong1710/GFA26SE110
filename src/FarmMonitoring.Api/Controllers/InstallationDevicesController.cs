using FarmMonitoring.Api.Authorization;
using FarmMonitoring.Api.Contracts;
using FarmMonitoring.Application.Features.Installations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace FarmMonitoring.Api.Controllers;
[ApiController]
[Route("api/installation-jobs/{jobId:int}/devices")]
[Authorize(Policy = AccessPolicies.OperationalUser)]
public sealed class InstallationDevicesController(InstallationDeviceService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AllocatedDeviceResponse>>>> Get(int jobId, CancellationToken ct) => Ok(ApiResponse<IReadOnlyList<AllocatedDeviceResponse>>.Ok(await service.GetAsync(jobId, ct)));
    [HttpPut]
    [Authorize(Policy = AccessPolicies.ReviewInstallations)]
    [RequestSizeLimit(524288)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AllocatedDeviceResponse>>>> Set(int jobId, JobDevicesRequest request, CancellationToken ct) => Ok(ApiResponse<IReadOnlyList<AllocatedDeviceResponse>>.Ok(await service.SetAsync(jobId, request, ct)));
}
