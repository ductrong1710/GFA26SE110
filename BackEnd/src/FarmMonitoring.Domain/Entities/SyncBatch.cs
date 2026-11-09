namespace FarmMonitoring.Domain.Entities;

public sealed class SyncBatch
{
    public int Id { get; set; }
    public int GatewayId { get; set; }
    public Gateway Gateway { get; set; } = null!;
    public int? MissionId { get; set; }
    public Mission? Mission { get; set; }
    public string BatchKey { get; set; } = "";
    public int RecordCount { get; set; }
    public int AcceptedCount { get; set; }
    public int DuplicateCount { get; set; }
    public int RejectedCount { get; set; }
    public string Status { get; set; } = "";
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public string PayloadHash { get; set; } = "";
    public string ResponseJson { get; set; } = "";
}
