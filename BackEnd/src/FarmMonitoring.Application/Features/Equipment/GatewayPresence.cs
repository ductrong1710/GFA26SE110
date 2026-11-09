namespace FarmMonitoring.Application.Features.Equipment;

public static class GatewayPresence
{
    public const int TimeoutSeconds = 90;
    public static string Status(bool active, DateTimeOffset? lastSeen, DateTimeOffset now) =>
        active && lastSeen.HasValue && lastSeen.Value <= now &&
        now - lastSeen.Value <= TimeSpan.FromSeconds(TimeoutSeconds) ? "ONLINE" : "OFFLINE";
}

public sealed record GatewayHeartbeatResponse(int GatewayId, string Status, DateTimeOffset LastSeenAt);
