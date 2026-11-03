using FarmMonitoring.Api.Authorization;
using FarmMonitoring.Api.Contracts;
using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.PublicInformation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmMonitoring.Api.Controllers;

[ApiController]
[Route("api/admin/public-content")]
[Authorize(Policy = AccessPolicies.ManageUsers)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PublicContentAdminController(PublicContentService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiPageResponse<ManagedEntry>>> List([FromQuery] PageQuery page, CancellationToken ct) =>
        Ok(ApiPageResponse<ManagedEntry>.From(await service.ManagedAsync(page, ct)));

    [HttpPost]
    [RequestSizeLimit(3 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<ManagedEntry>>> Create(PublicContentRequest request, CancellationToken ct) =>
        Ok(ApiResponse<ManagedEntry>.Ok(await service.SaveAsync(null, request, ct)));

    [HttpPut("{id:int}")]
    [RequestSizeLimit(3 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<ManagedEntry>>> Edit(int id, PublicContentRequest request, CancellationToken ct) =>
        Ok(ApiResponse<ManagedEntry>.Ok(await service.SaveAsync(id, request, ct)));

    [HttpPost("{id:int}/publish")]
    public async Task<ActionResult<ApiResponse<ManagedEntry>>> Publish(int id, PublicationRequest request, CancellationToken ct) =>
        Ok(ApiResponse<ManagedEntry>.Ok(await service.PublishAsync(id, request, true, ct)));

    [HttpPost("{id:int}/unpublish")]
    public async Task<ActionResult<ApiResponse<ManagedEntry>>> Unpublish(int id, PublicationRequest request, CancellationToken ct) =>
        Ok(ApiResponse<ManagedEntry>.Ok(await service.PublishAsync(id, request, false, ct)));
}
