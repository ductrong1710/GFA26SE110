using FarmMonitoring.Api.Contracts;
using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.PublicInformation;
using FarmMonitoring.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmMonitoring.Api.Controllers;

[ApiController]
[AllowAnonymous]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PublicContentController(PublicContentService service) : ControllerBase
{
    [HttpGet("/api/public-devices")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PublicEntry>>>> Devices(CancellationToken ct) =>
        Ok(ApiResponse<IReadOnlyList<PublicEntry>>.Ok(await service.PublishedAsync(PublicContentKind.DEVICE, ct)));

    [HttpGet("/api/public-contacts")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PublicEntry>>>> Contacts(CancellationToken ct) =>
        Ok(ApiResponse<IReadOnlyList<PublicEntry>>.Ok(await service.PublishedAsync(PublicContentKind.CONTACT, ct)));

    [HttpGet("/api/public-components")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PublicEntry>>>> Components(CancellationToken ct) =>
        Ok(ApiResponse<IReadOnlyList<PublicEntry>>.Ok(await service.PublishedAsync(PublicContentKind.COMPONENT, ct)));

    [HttpGet("/api/public-devices/{id:int}/image")]
    public async Task<IActionResult> Image(int id, CancellationToken ct)
    {
        var image = await service.ImageAsync(id, ct);
        return File(image.Content, image.ContentType);
    }
}
