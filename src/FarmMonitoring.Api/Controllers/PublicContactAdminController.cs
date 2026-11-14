using FarmMonitoring.Api.Authorization;
using FarmMonitoring.Api.Contracts;
using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.PublicInformation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmMonitoring.Api.Controllers;

[ApiController]
[Route("api/admin/public-contacts")]
[Authorize(Policy = AccessPolicies.ManageUsers)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PublicContactAdminController(PublicContentService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ManagedEntry>>>> List(CancellationToken ct) =>
        Ok(ApiResponse<IReadOnlyList<ManagedEntry>>.Ok(await service.ManagedContactsAsync(ct)));

    [HttpPost]
    public async Task<ActionResult<ApiResponse<ManagedEntry>>> Create(PublicContentRequest request, CancellationToken ct) =>
        Ok(ApiResponse<ManagedEntry>.Ok(await service.SaveContactAsync(null, request, ct)));

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse<ManagedEntry>>> Edit(int id, PublicContentRequest request, CancellationToken ct) =>
        Ok(ApiResponse<ManagedEntry>.Ok(await service.SaveContactAsync(id, request, ct)));

    [HttpPost("{id:int}/publish")]
    public async Task<ActionResult<ApiResponse<ManagedEntry>>> Publish(int id, PublicationRequest request, CancellationToken ct) =>
        Ok(ApiResponse<ManagedEntry>.Ok(await service.PublishContactAsync(id, request, true, ct)));

    [HttpPost("{id:int}/unpublish")]
    public async Task<ActionResult<ApiResponse<ManagedEntry>>> Unpublish(int id, PublicationRequest request, CancellationToken ct) =>
        Ok(ApiResponse<ManagedEntry>.Ok(await service.PublishContactAsync(id, request, false, ct)));
}
