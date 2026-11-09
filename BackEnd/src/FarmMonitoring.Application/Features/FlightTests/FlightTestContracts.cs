namespace FarmMonitoring.Application.Features.FlightTests;
public sealed record CreateFlightTestRequest(string RequestKey);
public sealed record CancelFlightTestRequest(string Reason, bool ConfirmAircraftGrounded, bool ConfirmNeverStarted);
public sealed record FlightTestResultRequest(string Status, bool? GatewayConnected, bool? UavConnected, bool? Landed, int? ObservedAltitudeCm, int? HeldMs, string? ErrorCode, bool? TakeoffAttempted);
public sealed record FlightTestResponse(int Id, int GatewayId, int UavId, string Status, int AltitudeCm, int HoldMs, DateTimeOffset ExpiresAt,
    bool? GatewayConnected, bool? UavConnected, bool? Landed, int? ObservedAltitudeCm, int? HeldMs, string? ErrorCode, bool? TakeoffAttempted);
