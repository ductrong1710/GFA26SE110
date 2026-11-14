using FarmMonitoring.Api.Authorization;
using FarmMonitoring.Api.Contracts;
using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.PublicInformation;
using FarmMonitoring.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmMonitoring.Api.Controllers;

[ApiController]
[Route("api/admin/public-{resource}")]
[Authorize(Policy = AccessPolicies.ManageUsers)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PublicContentAdminController(PublicContentService service) : ControllerBase
{
    private static readonly IReadOnlyDictionary<string, PublicContentKind> Kinds = new Dictionary<string, PublicContentKind>(StringComparer.OrdinalIgnoreCase)
    {
        ["devices"] = PublicContentKind.DEVICE, ["services"] = PublicContentKind.SERVICE,
        ["installations"] = PublicContentKind.INSTALLATION, ["warranties"] = PublicContentKind.WARRANTY,
        ["maintenance"] = PublicContentKind.MAINTENANCE, ["returns"] = PublicContentKind.RETURN,
        ["components"] = PublicContentKind.COMPONENT
    };
    private PublicContentKind Kind => Kinds.TryGetValue((string?)RouteData.Values["resource"] ?? "", out var kind)
        ? kind : throw new NotFoundException("Content resource not found.");

    [HttpGet]
    public async Task<ActionResult<ApiPageResponse<ManagedEntry>>> List([FromQuery] PageQuery page, CancellationToken ct) =>
        Ok(ApiPageResponse<ManagedEntry>.From(await service.ManagedAsync(page, Kind, ct)));

    [HttpPost]
    [RequestSizeLimit(3 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<ManagedEntry>>> Create(PublicContentRequest request, CancellationToken ct) =>
        Ok(ApiResponse<ManagedEntry>.Ok(await service.SaveAsync(null, request with { Kind = Kind.ToString() }, ct)));

    [HttpPut("{id:int}")]
    [RequestSizeLimit(3 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<ManagedEntry>>> Edit(int id, PublicContentRequest request, CancellationToken ct) =>
        Ok(ApiResponse<ManagedEntry>.Ok(await service.SaveForKindAsync(id, request with { Kind = Kind.ToString() }, Kind, ct)));

    [HttpPost("{id:int}/publish")]
    public async Task<ActionResult<ApiResponse<ManagedEntry>>> Publish(int id, PublicationRequest request, CancellationToken ct) =>
        Ok(ApiResponse<ManagedEntry>.Ok(await service.PublishForKindAsync(id, request, Kind, true, ct)));

    [HttpPost("{id:int}/unpublish")]
    public async Task<ActionResult<ApiResponse<ManagedEntry>>> Unpublish(int id, PublicationRequest request, CancellationToken ct) =>
        Ok(ApiResponse<ManagedEntry>.Ok(await service.PublishForKindAsync(id, request, Kind, false, ct)));
}
