using FarmMonitoring.Application.Features.Farms;
using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Entities;
using FluentValidation;

namespace FarmMonitoring.Application.Features.Thresholds;

public sealed class ThresholdService(FarmAccessService access, IThresholdRepository repository, IValidator<ThresholdRequest> validator, TimeProvider clock)
{
    public async Task<ThresholdResponse> GetAsync(int channelId, CancellationToken ct)
    {
        await access.EnsureAsync(FarmResource.SensorChannel, channelId, ct);
        return ToResponse(await repository.GetAsync(channelId, ct) ?? throw new NotFoundException("Threshold not found."));
    }

    public async Task<ThresholdResponse> SetAsync(int channelId, ThresholdRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        await access.EnsureAsync(FarmResource.SensorChannel, channelId, ct);
        var threshold = new SensorThreshold
        {
            SensorChannelId = channelId, MinValue = request.MinValue, MaxValue = request.MaxValue,
            DataTimeoutMinutes = request.DataTimeoutMinutes, LowBatteryPercent = request.LowBatteryPercent,
            IsEnabled = request.IsEnabled, CreatedAt = clock.GetUtcNow()
        };
        return ToResponse(await repository.UpsertAsync(threshold, ct) ?? throw new NotFoundException("Sensor channel not found."));
    }

    private static ThresholdResponse ToResponse(SensorThreshold x) => new(x.Id, x.SensorChannelId, x.MinValue, x.MaxValue,
        x.DataTimeoutMinutes, x.LowBatteryPercent, x.IsEnabled, x.CreatedAt, x.UpdatedAt);
}
