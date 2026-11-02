namespace FarmMonitoring.Domain.Entities;

public sealed class ServiceReport
{
    public int? ReplacementId { get; set; }
    public ServiceDeviceReplacement? Replacement { get; set; }
    public int Id { get; set; }
    public int JobId { get; set; }
    public ServiceJob Job { get; set; } = null!;
    public int Revision { get; set; }
    public int TechnicianId { get; set; }
    public string Cause { get; set; } = "";
    public string Work { get; set; } = "";
    public string Components { get; set; } = "";
    public string TestResults { get; set; } = "";
    public string Notes { get; set; } = "";
    public bool ConnectivityPassed { get; set; }
    public bool OperationPassed { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public int? ConfirmedByUserId { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
    public string? ConfirmationNote { get; set; }
    public int? ReviewedByUserId { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? ReviewNote { get; set; }
    public bool? Approved { get; set; }
    public List<EvidenceImage> Images { get; set; } = [];
}
