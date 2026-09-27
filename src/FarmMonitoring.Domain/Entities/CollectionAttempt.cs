namespace FarmMonitoring.Domain.Entities;

public sealed class CollectionAttempt
{
    public int Id { get; set; }
    public int MissionId { get; set; }
    public Mission Mission { get; set; } = null!;
    public int MissionTargetId { get; set; }
    public MissionTarget MissionTarget { get; set; } = null!;
    public int? GatewayId { get; set; }
    public Gateway? Gateway { get; set; }
    public int AttemptNo { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public string Status { get; set; } = "";
    public int RecordsReceived { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
}
