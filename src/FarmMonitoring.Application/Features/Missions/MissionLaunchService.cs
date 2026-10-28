using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Farms;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Entities;
using FluentValidation;

namespace FarmMonitoring.Application.Features.Missions;

public sealed class MissionLaunchService(IMissionRepository repository, FarmAccessService access,
    MissionLifecycle lifecycle, TimeProvider clock)
{
    private MissionLaunchResponse View(Mission m) => new(MissionLaunchRules.State(m, clock.GetUtcNow()),
        m.PlanRevision, m.ScheduledStartAt, MissionLaunchRules.ExpiresAt(m), m.LaunchReadyAt, m.LaunchApprovedAt, m.LaunchSession);

    public async Task<MissionLaunchResponse> GetAsync(int id, CancellationToken ct)
    {
        var m = await repository.FindAsync(id, false, ct) ?? throw new NotFoundException("Mission not found.");
        await access.EnsureFarmAccessAsync(m.FarmId, ct);
        return View(m);
    }

    public Task<MissionLaunchResponse> ApproveAsync(int id, ApproveMissionLaunchRequest request, int actor, CancellationToken ct) =>
        repository.InTransactionAsync(async () =>
        {
            var m = await repository.FindAsync(id, true, ct) ?? throw new NotFoundException("Mission not found.");
            await access.EnsureFarmAccessAsync(m.FarmId, ct);
            if (request.PlanRevision != m.PlanRevision || request.Session != m.LaunchSession)
                throw new ConflictException("The plan or gateway session changed. Review the current plan and confirm again.");
            if (!request.ConfirmAircraftReady || !request.ConfirmAreaClear)
                throw new ValidationException("Confirm the aircraft is ready on the ground and the flight area is clear.");
            var now = clock.GetUtcNow();
            if (!TelloMissionRules.Applies(m) || m.Status != MissionStatus.SCHEDULED ||
                m.ScheduledStartAt is null || now < m.ScheduledStartAt.Value.AddMinutes(-10) || now >= MissionLaunchRules.ExpiresAt(m))
                throw new ConflictException("Confirm within 10 minutes before the scheduled start and before the launch window expires.");
            if (MissionLaunchRules.State(m, now) is not ("AWAITING_CONFIRMATION" or "WAITING_FOR_TIME" or "READY_TO_START"))
                throw new ConflictException("Gateway must report a fresh, downloaded plan before confirmation.");
            m.LaunchApprovedAt = now;
            lifecycle.Log(m, actor, null, "LAUNCH_APPROVED", "Aircraft readiness and clear flight area confirmed for the current gateway session.");
            await repository.SaveAsync(ct);
            return View(m);
        }, ct);

    public Task<MissionLaunchResponse> DeviceAsync(int id, int gatewayId, int authenticatedGateway,
        MissionLaunchRequest request, bool consume, CancellationToken ct) => repository.InTransactionAsync(async () =>
        {
            if (gatewayId != authenticatedGateway) throw new AccessDeniedException("Gateway identity does not match the route.");
            if (request.Session is null || request.Session.Length != 32 || request.Session.Any(c => !Uri.IsHexDigit(c)))
                throw new ValidationException("A 32-character hexadecimal boot session is required.");
            var m = await repository.FindAsync(id, true, ct) ?? throw new NotFoundException("Mission not found.");
            if (m.GatewayId != gatewayId) throw new AccessDeniedException("Gateway is not assigned to this mission.");
            if (m.UavId is int uavId) m.Uav = await repository.LockUavAsync(uavId, ct);
            if (await repository.LockGatewayAsync(gatewayId, ct) is not { IsActive: true })
                throw new AccessDeniedException("Gateway is inactive.");
            if (request.PlanRevision != m.PlanRevision) throw new ConflictException("PLAN_CHANGED_DOWNLOAD_AGAIN");
            if (m.Status != MissionStatus.SCHEDULED || m.LaunchConsumedAt.HasValue)
                return View(m); // A consumed launch is never returned as executable, even on retry.
            if (!TelloMissionRules.Applies(m) || !await repository.HasDeviceLogAsync(id, gatewayId, "DOWNLOADED", ct))
                throw new ConflictException("A downloaded Tello mission is required.");
            var now = clock.GetUtcNow();
            if (consume)
            {
                if (m.LaunchSession != request.Session || MissionLaunchRules.State(m, now) != "READY_TO_START")
                    throw new ConflictException("Launch is not approved, not due, expired, or belongs to another gateway session.");
                await lifecycle.ChangeStatusAsync(m, MissionStatus.RUNNING, new(null, null, null, null), null, gatewayId, ct);
                m.LaunchConsumedAt = now;
                lifecycle.Log(m, null, gatewayId, "LAUNCH_CONSUMED", "Gateway accepted the scheduled launch once; takeoff is not yet confirmed.");
                await repository.SaveAsync(ct);
                return View(m) with { State = "START_ACCEPTED" };
            }
            if (now >= MissionLaunchRules.ExpiresAt(m))
            {
                await lifecycle.ChangeStatusAsync(m, MissionStatus.CANCELLED, new(null, null, null, "MISSED_LAUNCH_WINDOW"), null, gatewayId, ct);
                await repository.SaveAsync(ct);
                return View(m) with { State = "MISSED" };
            }
            if (m.LaunchSession != request.Session)
            {
                m.LaunchApprovedAt = null;
                m.LaunchSession = request.Session;
            }
            m.LaunchReadyAt = now;
            await repository.SaveAsync(ct);
            return View(m);
        }, ct);
}
