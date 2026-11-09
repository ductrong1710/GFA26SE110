using System.Reflection;
using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.FlightTests;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Entities;

namespace FarmMonitoring.UnitTests;

public class FlightTestRecoveryTests
{
    [Fact]
    public async Task Confirmed_landing_resolves_recovery_without_erasing_original_failure()
    {
        var saved = new FlightTest { Id = 1, GatewayId = 2, Status = FlightTestStatus.RECOVERY_REQUIRED,
            GatewayConnected = true, UavConnected = false, Landed = false, TakeoffAttempted = true,
            ObservedAltitudeCm = 70, HeldMs = 0, ErrorCode = "LINK_LOST" };
        var repository = DispatchProxy.Create<IFlightTestRepository, RecoveryRepository>();
        ((RecoveryRepository)(object)repository).Saved = saved;
        var service = new FlightTestService(repository, null!, null!, TimeProvider.System);
        var request = new FlightTestResultRequest("FAILED", true, true, true, 70, 0, "LINK_LOST", true);
        await Assert.ThrowsAsync<ConflictException>(() => service.ResultAsync(2, 2, 1, request with { ObservedAltitudeCm = 0 }, default));
        var result = await service.ResultAsync(2, 2, 1, request, default);
        Assert.Equal("FAILED", result.Status);
        Assert.True(result.Landed);
        Assert.Equal("LINK_LOST", result.ErrorCode);
        Assert.Equal(result, await service.ResultAsync(2, 2, 1, request, default));
    }

    public class RecoveryRepository : DispatchProxy
    {
        public FlightTest Saved = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name switch
        {
            "InTransactionAsync" => ((Delegate)args![0]!).DynamicInvoke(),
            "FindAsync" => Task.FromResult<FlightTest?>(Saved),
            "SaveAsync" => Task.CompletedTask,
            _ => throw new NotSupportedException(method.Name)
        };
    }
}
