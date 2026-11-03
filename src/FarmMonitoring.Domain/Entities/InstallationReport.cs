namespace FarmMonitoring.Domain.Entities;

public sealed class InstallationReport
{
    public int Id { get; set; }
    public int JobId { get; set; }
    public InstallationJob Job { get; set; } = null!;
    public int Revision { get; set; }
    public int TechnicianId { get; set; }
    public string Notes { get; set; } = "";
    public string? Issues { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public int? ConfirmedByUserId { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
    public string? ConfirmationNote { get; set; }
    public int? ReviewedByUserId { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? ReviewNote { get; set; }
    public bool? Approved { get; set; }
    public string? PaperworkJson { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public List<InstalledDeviceCheck> Devices { get; set; } = [];
    public List<EvidenceImage> Images { get; set; } = [];
}
public sealed class InstalledDeviceCheck
{
    public int Id { get; set; }
    public int ReportId { get; set; }
    public InstallationReport Report { get; set; } = null!;
    public int ProposalLineId { get; set; }
    public int? SensorNodeId { get; set; }
    public int? UavId { get; set; }
    public int? GatewayId { get; set; }
    public string InstallationLocation { get; set; } = "";
    public bool AccessoriesChecked { get; set; }
    public bool ConnectivityPassed { get; set; }
    public bool OperationPassed { get; set; }
    public string TestResults { get; set; } = "";
}
