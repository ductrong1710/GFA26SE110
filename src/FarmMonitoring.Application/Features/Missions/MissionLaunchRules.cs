using FarmMonitoring.Domain.Entities;

namespace FarmMonitoring.Application.Features.Missions;

public static class MissionLaunchRules
{
    public static DateTimeOffset? ExpiresAt(Mission m) => m.ScheduledStartAt is { } start && m.ScheduledEndAt is { } end
        ? (start.AddSeconds(60) < end ? start.AddSeconds(60) : end) : null;

    public static string State(Mission m, DateTimeOffset now)
    {
        if (m.Status is MissionStatus.COMPLETED or MissionStatus.FAILED or MissionStatus.CANCELLED) return "TERMINAL";
        if (m.LaunchConsumedAt.HasValue) return "CONSUMED";
        if (m.Status != MissionStatus.SCHEDULED || !m.ScheduledStartAt.HasValue) return "UNAVAILABLE";
        if (now >= ExpiresAt(m)) return "MISSED";
        if (m.LaunchSession is null || m.LaunchReadyAt is not { } ready || ready < now.AddSeconds(-30) || ready > now)
            return "WAITING_FOR_GATEWAY";
        if (m.LaunchApprovedAt is null) return "AWAITING_CONFIRMATION";
        return now < m.ScheduledStartAt ? "WAITING_FOR_TIME" : "READY_TO_START";
    }

    public static void Invalidate(Mission m)
    {
        m.PlanRevision++;
        m.LaunchSession = null;
        m.LaunchReadyAt = m.LaunchApprovedAt = m.LaunchConsumedAt = null;
    }
}

public sealed record MissionLaunchRequest(string Session, int PlanRevision);
public sealed record ApproveMissionLaunchRequest(bool ConfirmAircraftReady, bool ConfirmAreaClear, int PlanRevision, string Session);
public sealed record MissionLaunchResponse(string State, int PlanRevision, DateTimeOffset? ScheduledStartAt,
    DateTimeOffset? ExpiresAt, DateTimeOffset? ReadyAt, DateTimeOffset? ApprovedAt, string? Session);
