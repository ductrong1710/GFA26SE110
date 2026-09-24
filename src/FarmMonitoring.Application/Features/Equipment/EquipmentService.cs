using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Entities;
using FluentValidation;

namespace FarmMonitoring.Application.Features.Equipment;

public sealed class EquipmentService(IEquipmentRepository repository, TimeProvider clock, IValidator<PageQuery> pages,
    IValidator<UavRequest> uavs, IValidator<GatewayRequest> gateways, IValidator<EquipmentStatusRequest> statuses,
    IValidator<AssignUavRequest> assignments)
{
    public async Task<PagedResult<UavResponse>> ListUavsAsync(PageQuery query, CancellationToken ct)
    {
        await pages.ValidateAndThrowAsync(query, ct);
        return await repository.ListUavsAsync(query, ct);
    }
    public async Task<PagedResult<GatewayResponse>> ListGatewaysAsync(PageQuery query, CancellationToken ct)
    {
        await pages.ValidateAndThrowAsync(query, ct);
        return await repository.ListGatewaysAsync(query, ct);
    }
    public async Task<UavResponse> GetUavAsync(int id, CancellationToken ct) => ToResponse(await RequireUavAsync(id, ct));
    public async Task<GatewayResponse> GetGatewayAsync(int id, CancellationToken ct) => ToResponse(await RequireGatewayAsync(id, ct));
    public async Task<UavResponse> CreateUavAsync(UavRequest request, CancellationToken ct)
    {
        await uavs.ValidateAndThrowAsync(request, ct);
        var uav = new Uav { CreatedAt = clock.GetUtcNow() };
        Apply(uav, request);
        repository.AddUav(uav);
        await repository.SaveAsync(ct);
        return ToResponse(uav);
    }
    public async Task<UavResponse> UpdateUavAsync(int id, UavRequest request, CancellationToken ct)
    {
        await uavs.ValidateAndThrowAsync(request, ct);
        var uav = await RequireUavAsync(id, ct);
        Apply(uav, request);
        uav.UpdatedAt = clock.GetUtcNow();
        await repository.SaveAsync(ct);
        return ToResponse(uav);
    }
    public async Task<UavResponse> SetUavStatusAsync(int id, EquipmentStatusRequest request, CancellationToken ct)
    {
        await statuses.ValidateAndThrowAsync(request, ct);
        var uav = await RequireUavAsync(id, ct);
        uav.Status = request.Status.Trim();
        uav.IsActive = request.IsActive!.Value;
        uav.UpdatedAt = clock.GetUtcNow();
        await repository.SaveAsync(ct);
        return ToResponse(uav);
    }
    public async Task<GatewayResponse> CreateGatewayAsync(GatewayRequest request, CancellationToken ct)
    {
        await gateways.ValidateAndThrowAsync(request, ct);
        if (request.UavId is int uavId) await RequireUavAsync(uavId, ct);
        var gateway = new Gateway { CreatedAt = clock.GetUtcNow() };
        Apply(gateway, request);
        repository.AddGateway(gateway);
        await repository.SaveAsync(ct);
        return ToResponse(gateway);
    }
    public async Task<GatewayResponse> UpdateGatewayAsync(int id, GatewayRequest request, CancellationToken ct)
    {
        await gateways.ValidateAndThrowAsync(request, ct);
        var gateway = await RequireGatewayAsync(id, ct);
        if (request.UavId is int uavId) await RequireUavAsync(uavId, ct);
        Apply(gateway, request);
        gateway.UpdatedAt = clock.GetUtcNow();
        await repository.SaveAsync(ct);
        return ToResponse(gateway);
    }
    public async Task<GatewayResponse> SetGatewayStatusAsync(int id, EquipmentStatusRequest request, CancellationToken ct)
    {
        await statuses.ValidateAndThrowAsync(request, ct);
        var gateway = await RequireGatewayAsync(id, ct);
        gateway.Status = request.Status.Trim();
        gateway.IsActive = request.IsActive!.Value;
        gateway.UpdatedAt = clock.GetUtcNow();
        await repository.SaveAsync(ct);
        return ToResponse(gateway);
    }
    public async Task<GatewayResponse> AssignUavAsync(int id, AssignUavRequest request, CancellationToken ct)
    {
        await assignments.ValidateAndThrowAsync(request, ct);
        var gateway = await RequireGatewayAsync(id, ct);
        if (request.UavId is int uavId) await RequireUavAsync(uavId, ct);
        gateway.UavId = request.UavId;
        gateway.UpdatedAt = clock.GetUtcNow();
        await repository.SaveAsync(ct);
        return ToResponse(gateway);
    }
    private async Task<Uav> RequireUavAsync(int id, CancellationToken ct) =>
        await repository.FindUavAsync(id, ct) ?? throw new NotFoundException("UAV not found.");
    private async Task<Gateway> RequireGatewayAsync(int id, CancellationToken ct) =>
        await repository.FindGatewayAsync(id, ct) ?? throw new NotFoundException("Gateway not found.");
    private static void Apply(Uav uav, UavRequest request)
    {
        uav.Code = request.Code.Trim(); uav.Name = request.Name.Trim(); uav.Model = request.Model;
        uav.Status = request.Status.Trim(); uav.BatteryPercent = request.BatteryPercent;
        uav.LastSeenAt = request.LastSeenAt?.ToUniversalTime();
    }
    private static void Apply(Gateway gateway, GatewayRequest request)
    {
        gateway.Code = request.Code.Trim(); gateway.Name = request.Name.Trim(); gateway.GatewayType = request.GatewayType.Trim();
        gateway.Status = request.Status.Trim(); gateway.UavId = request.UavId; gateway.BatteryPercent = request.BatteryPercent;
        gateway.LastSeenAt = request.LastSeenAt?.ToUniversalTime(); gateway.FirmwareVersion = request.FirmwareVersion;
    }
    private static UavResponse ToResponse(Uav x) => new(x.Id, x.Code, x.Name, x.Model, x.Status, x.BatteryPercent, x.LastSeenAt, x.IsActive, x.CreatedAt, x.UpdatedAt);
    private static GatewayResponse ToResponse(Gateway x) => new(x.Id, x.Code, x.Name, x.GatewayType, x.Status, x.UavId, x.BatteryPercent, x.LastSeenAt, x.FirmwareVersion, x.IsActive, x.CreatedAt, x.UpdatedAt);
}
