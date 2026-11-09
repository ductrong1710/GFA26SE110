namespace FarmMonitoring.Domain.Entities;
public enum FlightTestStatus { QUEUED, CLAIMED, COMPLETED, FAILED, RECOVERY_REQUIRED, EXPIRED, CANCELLED }
public sealed class FlightTest
{
    public int Id { get; set; }
    public int GatewayId { get; set; }
    public int UavId { get; set; }
    public int RequestedByUserId { get; set; }
    public string RequestKey { get; set; } = "";
    public FlightTestStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ClaimedAt { get; set; }
    public DateTimeOffset? ReportedAt { get; set; }
    public bool? GatewayConnected { get; set; }
    public bool? UavConnected { get; set; }
    public bool? Landed { get; set; }
    public bool? TakeoffAttempted { get; set; }
    public int? ObservedAltitudeCm { get; set; }
    public int? HeldMs { get; set; }
    public string? ErrorCode { get; set; }
}
