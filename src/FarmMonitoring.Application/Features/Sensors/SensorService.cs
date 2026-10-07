using FarmMonitoring.Application.Features.Farms;
using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Entities;
using FluentValidation;

namespace FarmMonitoring.Application.Features.Sensors;

public sealed class SensorService(FarmAccessService access, ISensorRepository repository, TimeProvider clock,
    IValidator<PageQuery> pages, IValidator<SensorTypeRequest> types, IValidator<SensorNodeRequest> nodes,
    IValidator<SensorNodeStatusRequest> statuses, IValidator<SensorChannelRequest> channels)
{
    public async Task<PagedResult<SensorTypeResponse>> ListTypesAsync(PageQuery query, CancellationToken ct)
    {
        await pages.ValidateAndThrowAsync(query, ct);
        return await repository.ListTypesAsync(query, ct);
    }
    public async Task<SensorTypeResponse> CreateTypeAsync(SensorTypeRequest request, CancellationToken ct)
    {
        await types.ValidateAndThrowAsync(request, ct);
        var type = new SensorType { Code = request.Code.Trim(), Name = request.Name.Trim(), Unit = request.Unit, Description = request.Description };
        repository.AddType(type);
        await repository.SaveAsync(ct);
        return new(type.Id, type.Code, type.Name, type.Unit, type.Description);
    }
    public async Task<PagedResult<SensorNodeResponse>> ListNodesAsync(SensorNodeQuery query, CancellationToken ct)
    {
        await pages.ValidateAndThrowAsync(query, ct);
        await access.CheckFilterAsync(FarmResource.Farm, query.FarmId, ct);
        await access.CheckFilterAsync(FarmResource.Zone, query.ZoneId, ct);
        return await repository.ListNodesAsync(query, await access.GetScopeAsync(ct), ct);
    }
    public async Task<SensorNodeResponse> GetNodeAsync(int id, CancellationToken ct) => ToResponse(await RequireNodeAsync(id, ct));
    public async Task<SensorNodeResponse> CreateNodeAsync(SensorNodeRequest request, CancellationToken ct)
    {
        await nodes.ValidateAndThrowAsync(request, ct);
        await RequireZoneAsync(request.ZoneId, ct);
        var node = new SensorNode { CreatedAt = clock.GetUtcNow() };
        Apply(node, request);
        repository.AddNode(node);
        await repository.SaveAsync(ct);
        return ToResponse(node);
    }
    public async Task<SensorNodeResponse> UpdateNodeAsync(int id, SensorNodeRequest request, CancellationToken ct)
    {
        await nodes.ValidateAndThrowAsync(request, ct);
        return await repository.InTransactionAsync(async () =>
        {
            var node = await repository.LockNodeAsync(id, ct) ?? throw new NotFoundException("Sensor node not found.");
            await access.EnsureAsync(FarmResource.SensorNode, id, ct);
            await RequireZoneAsync(request.ZoneId, ct);
            if (node.ZoneId != request.ZoneId && await repository.ConflictsWithMissionAsync(id, request.ZoneId, ct))
                throw new ConflictException("A sensor selected for an unfinished mission cannot move to another farm.");
            Apply(node, request);
            node.UpdatedAt = clock.GetUtcNow();
            await repository.SaveAsync(ct);
            return ToResponse(node);
        }, ct);
    }
    public async Task<SensorNodeResponse> SetStatusAsync(int id, SensorNodeStatusRequest request, CancellationToken ct)
    {
        await statuses.ValidateAndThrowAsync(request, ct);
        var node = await RequireNodeAsync(id, ct);
        node.Status = request.Status.Trim();
        node.IsActive = request.IsActive!.Value;
        node.UpdatedAt = clock.GetUtcNow();
        await repository.SaveAsync(ct);
        return ToResponse(node);
    }
    public async Task<PagedResult<SensorChannelResponse>> ListChannelsAsync(int nodeId, PageQuery query, CancellationToken ct)
    {
        await pages.ValidateAndThrowAsync(query, ct);
        await RequireNodeAsync(nodeId, ct);
        return await repository.ListChannelsAsync(nodeId, query, ct);
    }
    public async Task<SensorChannelResponse> CreateChannelAsync(int nodeId, SensorChannelRequest request, CancellationToken ct)
    {
        await channels.ValidateAndThrowAsync(request, ct);
        await RequireNodeAsync(nodeId, ct);
        await RequireTypeAsync(request.SensorTypeId, ct);
        var channel = new SensorChannel { SensorNodeId = nodeId, CreatedAt = clock.GetUtcNow() };
        Apply(channel, request);
        repository.AddChannel(channel);
        await repository.SaveAsync(ct);
        return ToResponse(channel);
    }
    public async Task<SensorChannelResponse> UpdateChannelAsync(int id, SensorChannelRequest request, CancellationToken ct)
    {
        await channels.ValidateAndThrowAsync(request, ct);
        return await repository.InTransactionAsync(async () =>
        {
            var channel = await repository.LockChannelAsync(id, ct) ?? throw new NotFoundException("Sensor channel not found.");
            await access.EnsureAsync(FarmResource.SensorChannel, id, ct);
            await RequireTypeAsync(request.SensorTypeId, ct);
            if (channel.SensorTypeId != request.SensorTypeId && await repository.HasReadingsAsync(id, ct))
                throw new ConflictException("A channel with historical readings cannot change its measurement type.");
            Apply(channel, request);
            channel.UpdatedAt = clock.GetUtcNow();
            await repository.SaveAsync(ct);
            return ToResponse(channel);
        }, ct);
    }
    private async Task<SensorNode> RequireNodeAsync(int id, CancellationToken ct)
    {
        await access.EnsureAsync(FarmResource.SensorNode, id, ct);
        return await repository.FindNodeAsync(id, ct) ?? throw new NotFoundException("Sensor node not found.");
    }
    private async Task RequireZoneAsync(int id, CancellationToken ct)
    {
        await access.EnsureAsync(FarmResource.Zone, id, ct);
    }
    private async Task RequireTypeAsync(int id, CancellationToken ct)
    {
        if (!await repository.TypeExistsAsync(id, ct)) throw new NotFoundException("Sensor type not found.");
    }
    private static void Apply(SensorNode node, SensorNodeRequest request)
    {
        node.ZoneId = request.ZoneId;
        node.DeviceCode = request.DeviceCode.Trim();
        node.Name = request.Name.Trim();
        node.Status = request.Status.Trim();
        node.Latitude = request.Latitude;
        node.Longitude = request.Longitude;
        node.LocalX = request.LocalX;
        node.LocalY = request.LocalY;
        node.LastSeenAt = request.LastSeenAt?.ToUniversalTime();
        node.BatteryPercent = request.BatteryPercent;
    }
    private static void Apply(SensorChannel channel, SensorChannelRequest request)
    {
        channel.SensorTypeId = request.SensorTypeId;
        channel.ChannelCode = request.ChannelCode.Trim();
        channel.Name = request.Name;
        channel.IsActive = request.IsActive;
    }
    private static SensorNodeResponse ToResponse(SensorNode x) => new(x.Id, x.ZoneId, x.DeviceCode, x.Name, x.Status,
        x.Latitude, x.Longitude, x.LocalX, x.LocalY, x.LastSeenAt, x.BatteryPercent, x.IsActive, x.CreatedAt, x.UpdatedAt);
    private static SensorChannelResponse ToResponse(SensorChannel x) => new(x.Id, x.SensorNodeId, x.SensorTypeId,
        x.ChannelCode, x.Name, x.IsActive, x.CreatedAt, x.UpdatedAt);
}
