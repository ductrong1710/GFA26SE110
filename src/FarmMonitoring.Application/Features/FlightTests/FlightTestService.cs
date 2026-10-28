using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Farms;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Entities;
using FluentValidation;
namespace FarmMonitoring.Application.Features.FlightTests;
public sealed class FlightTestService(IFlightTestRepository repository, FarmAccessService access, ICurrentUser user, TimeProvider clock)
{
    public async Task<FlightTestResponse> CreateAsync(int gatewayId, CreateFlightTestRequest request, CancellationToken ct)
    {
        await access.EnsureAdministratorAsync(ct);
        if (string.IsNullOrWhiteSpace(request.RequestKey) || request.RequestKey.Length > 100 || request.RequestKey.Any(char.IsControl)) throw new ValidationException("requestKey must contain 1 to 100 non-control characters.");
        return await repository.InTransactionAsync(async () => {
            var old = await repository.FindRequestAsync(gatewayId, request.RequestKey, ct);
            if (old is not null) { await Expire(old, ct); return Response(old); }
            var gateway = await repository.GatewayAsync(gatewayId, ct) ?? throw new NotFoundException("Gateway not found.");
            if (gateway.UavId is not int uavId) throw new BusinessRuleException("Gateway requires an assigned UAV.");
            await EnsureAvailable(gatewayId, uavId, 0, ct);
            var test = new FlightTest { GatewayId=gatewayId, UavId=uavId, RequestedByUserId=user.UserId!.Value, RequestKey=request.RequestKey,
                Status=FlightTestStatus.QUEUED, CreatedAt=clock.GetUtcNow(), ExpiresAt=clock.GetUtcNow().AddSeconds(120) };
            repository.Add(test); await repository.SaveAsync(ct); return Response(test);
        }, ct);
    }
    public async Task<FlightTestResponse> GetAsync(int gatewayId, int id, CancellationToken ct)
    {
        await access.EnsureAdministratorAsync(ct);
        return await repository.InTransactionAsync(async () => { var test=await Require(gatewayId,id,ct); await Expire(test,ct); return Response(test); },ct);
    }
    public async Task<FlightTestResponse?> NextAsync(int gatewayId, int authenticatedGateway, CancellationToken ct)
    {
        Authenticate(gatewayId,authenticatedGateway);
        var test=await repository.NextAsync(gatewayId,clock.GetUtcNow(),ct);
        return test is null ? null : Response(test);
    }
    public async Task<FlightTestResponse> CancelAsync(int gatewayId, int id, CancelFlightTestRequest request, CancellationToken ct)
    {
        await access.EnsureAdministratorAsync(ct);
        if (!request.ConfirmAircraftGrounded || !request.ConfirmNeverStarted ||
            string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 60 || request.Reason.Any(char.IsControl))
            throw new ValidationException("Confirm the aircraft is grounded and the test was never started by firmware; provide a reason of 1 to 60 characters.");
        return await repository.InTransactionAsync(async () => {
            var test = await Require(gatewayId, id, ct);
            if (test.Status == FlightTestStatus.CANCELLED) return Response(test);
            if (test.Status is not (FlightTestStatus.QUEUED or FlightTestStatus.CLAIMED or FlightTestStatus.EXPIRED) ||
                test.TakeoffAttempted == true || test.ObservedAltitudeCm > 0 || test.HeldMs > 0)
                throw new ConflictException("Only a flight test that never started can be cancelled. Flight or recovery evidence must be resolved by the gateway.");
            test.Status = FlightTestStatus.CANCELLED;
            // Preserve telemetry as-is: this is an operator cancellation, not device flight evidence.
            test.ErrorCode = $"ADMIN_CANCELLED:{user.UserId}:{request.Reason.Trim()}";
            test.ReportedAt = clock.GetUtcNow();
            await repository.SaveAsync(ct);
            return Response(test);
        }, ct);
    }
    public async Task<FlightTestResponse> ClaimAsync(int gatewayId, int authenticatedGateway, int id, CancellationToken ct)
    {
        Authenticate(gatewayId,authenticatedGateway);
        return await repository.InTransactionAsync(async () => {
            var test=await Require(gatewayId,id,ct);
            if (test.Status==FlightTestStatus.CLAIMED) return Response(test);
            if (test.Status!=FlightTestStatus.QUEUED || test.ExpiresAt<=clock.GetUtcNow()) throw new ConflictException("Flight test is not claimable.");
            await EnsureAvailable(gatewayId,test.UavId,id,ct);
            test.Status=FlightTestStatus.CLAIMED; test.ClaimedAt=clock.GetUtcNow(); await repository.SaveAsync(ct); return Response(test);
        },ct);
    }
    public async Task<FlightTestResponse> ResultAsync(int gatewayId, int authenticatedGateway, int id, FlightTestResultRequest request, CancellationToken ct)
    {
        Authenticate(gatewayId,authenticatedGateway);
        if (request.Status is not ("COMPLETED" or "FAILED" or "RECOVERY_REQUIRED") || request.GatewayConnected is not true || request.UavConnected is null || request.Landed is null || request.TakeoffAttempted is null ||
            request.ObservedAltitudeCm is null or <0 or >500 || request.HeldMs is null or <0 or >60000 || request.ErrorCode?.Length>100 || request.ErrorCode?.Any(char.IsControl)==true)
            throw new ValidationException("Invalid flight-test result.");
        if (request.Status=="COMPLETED" && (request.TakeoffAttempted!=true || request.GatewayConnected!=true || request.UavConnected!=true || request.Landed!=true || request.ObservedAltitudeCm is not (>=97 and <=103) || request.HeldMs is not (>=3000 and <=60000) || request.ErrorCode is not null))
            throw new ValidationException("Completion requires both connections, altitude within 97..103 cm, a 3000 ms hold and confirmed landing.");
        if (request.Status=="FAILED" && request.TakeoffAttempted==true && request.Landed!=true) throw new ValidationException("Unconfirmed landing requires RECOVERY_REQUIRED.");
        if (request.TakeoffAttempted==false && (request.ObservedAltitudeCm!=0 || request.HeldMs!=0)) throw new ValidationException("A test without takeoff cannot report flight evidence.");
        if (request.Status!="COMPLETED" && string.IsNullOrWhiteSpace(request.ErrorCode)) throw new ValidationException("An errorCode is required.");
        return await repository.InTransactionAsync(async () => {
            var test=await Require(gatewayId,id,ct);
            var interruptedBeforeTakeoff = request.Status=="FAILED" && request.TakeoffAttempted==false &&
                test.Status is FlightTestStatus.QUEUED or FlightTestStatus.EXPIRED;
            // Recovery confirms landing; it cannot rewrite the original flight or failure evidence.
            var recovered = test.Status == FlightTestStatus.RECOVERY_REQUIRED && request.Status == "FAILED" &&
                request.Landed == true && request.UavConnected == true && request.TakeoffAttempted == test.TakeoffAttempted &&
                request.ObservedAltitudeCm == test.ObservedAltitudeCm && request.HeldMs == test.HeldMs && request.ErrorCode == test.ErrorCode;
            if (test.Status!=FlightTestStatus.CLAIMED && !interruptedBeforeTakeoff && !recovered) {
                if (test.Status.ToString()==request.Status && test.TakeoffAttempted==request.TakeoffAttempted && test.GatewayConnected==request.GatewayConnected && test.UavConnected==request.UavConnected && test.Landed==request.Landed && test.ObservedAltitudeCm==request.ObservedAltitudeCm && test.HeldMs==request.HeldMs && test.ErrorCode==request.ErrorCode) return Response(test);
                throw new ConflictException("Result conflicts with the recorded state.");
            }
            test.Status=Enum.Parse<FlightTestStatus>(request.Status); test.GatewayConnected=request.GatewayConnected; test.UavConnected=request.UavConnected; test.Landed=request.Landed;
            test.ObservedAltitudeCm=request.ObservedAltitudeCm; test.HeldMs=request.HeldMs; test.ErrorCode=request.ErrorCode; test.ReportedAt=clock.GetUtcNow();
            test.TakeoffAttempted=request.TakeoffAttempted;
            await repository.SaveAsync(ct); return Response(test);
        },ct);
    }
    private async Task EnsureAvailable(int gatewayId,int uavId,int excludingId,CancellationToken ct)
    {
        var gateway=await repository.GatewayAsync(gatewayId,ct) ?? throw new NotFoundException("Gateway not found.");
        var uav=await repository.UavAsync(uavId,ct) ?? throw new NotFoundException("UAV not found.");
        if (!gateway.IsActive || !uav.IsActive || gateway.UavId!=uavId) throw new BusinessRuleException("Equipment is inactive or reassigned.");
        if (await repository.HasConflictAsync(gatewayId,uavId,excludingId,clock.GetUtcNow(),ct)) throw new ConflictException("Equipment is reserved.");
    }
    private static void Authenticate(int gatewayId,int authenticated) { if (gatewayId!=authenticated) throw new AccessDeniedException("Gateway identity mismatch."); }
    private async Task<FlightTest> Require(int gatewayId,int id,CancellationToken ct) { var test=await repository.FindAsync(id,ct) ?? throw new NotFoundException("Flight test not found."); if(test.GatewayId!=gatewayId) throw new AccessDeniedException("Gateway identity mismatch."); return test; }
    private async Task Expire(FlightTest test,CancellationToken ct) { if(test.Status==FlightTestStatus.QUEUED && test.ExpiresAt<=clock.GetUtcNow()) { test.Status=FlightTestStatus.EXPIRED; await repository.SaveAsync(ct); } }
    private static FlightTestResponse Response(FlightTest t)=>new(t.Id,t.GatewayId,t.UavId,t.Status.ToString(),100,3000,t.ExpiresAt,t.GatewayConnected,t.UavConnected,t.Landed,t.ObservedAltitudeCm,t.HeldMs,t.ErrorCode,t.TakeoffAttempted);
}
