namespace FarmMonitoring.Domain.Entities;

public enum InstallationJobStatus { ASSIGNED, IN_PROGRESS, REPORT_SUBMITTED, OWNER_CONFIRMED, APPROVED, REWORK }
public enum InstallationJobAction { ASSIGNED, REASSIGNED, STARTED, REPORT_SUBMITTED, OWNER_CONFIRMED, APPROVED, RETURNED, DEVICES_ALLOCATED }
public sealed class InstallationJob
{
    public int Id { get; set; }
    public int InstallationRequestId { get; set; }
    public InstallationRequest InstallationRequest { get; set; } = null!;
    public int TechnicianId { get; set; }
    public DateTimeOffset ScheduledAt { get; set; }
    public InstallationJobStatus Status { get; set; }
    public int Revision { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public List<InstallationJobHistory> History { get; set; } = [];
}
public sealed class InstallationJobHistory
{
    public int Id { get; set; }
    public int JobId { get; set; }
    public InstallationJob Job { get; set; } = null!;
    public InstallationJobAction Action { get; set; }
    public int UserId { get; set; }
    public int TechnicianId { get; set; }
    public DateTimeOffset ScheduledAt { get; set; }
    public string Note { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}
