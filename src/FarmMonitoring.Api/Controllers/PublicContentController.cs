using FarmMonitoring.Api.Contracts;
using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.PublicInformation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmMonitoring.Api.Controllers;

[ApiController]
[Route("api/public-content")]
[AllowAnonymous]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PublicContentController(PublicContentService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiPageResponse<PublicEntry>>> List([FromQuery] PageQuery page, CancellationToken ct) =>
        Ok(ApiPageResponse<PublicEntry>.From(await service.PublishedAsync(page, ct)));

    [HttpGet("{id:int}/image")]
    public async Task<IActionResult> Image(int id, CancellationToken ct)
    {
        var image = await service.ImageAsync(id, ct);
        return File(image.Content, image.ContentType);
    }
}
