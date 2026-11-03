namespace FarmMonitoring.Domain.Entities;

public enum ServiceJobStatus { ASSIGNED, IN_PROGRESS, REPORT_SUBMITTED, OWNER_CONFIRMED, REWORK, CLOSED }
public enum ServiceJobType { MAINTENANCE, REPAIR }
public enum ServiceJobAction { ASSIGNED, REASSIGNED, STARTED, REPORT_SUBMITTED, OWNER_CONFIRMED, RETURNED, CLOSED, DEVICE_REPLACED, PLAN_PROPOSED, PLAN_APPROVED, PLAN_REJECTED, PLAN_EXECUTED }
public sealed class ServiceJob
{
    public int Id { get; set; }
    public int? IncidentId { get; set; }
    public DeviceIncident? Incident { get; set; }
    public int RentalDeviceAssignmentId { get; set; }
    public RentalDeviceAssignment RentalDeviceAssignment { get; set; } = null!;
    public ServiceJobType Type { get; set; }
    public int TechnicianId { get; set; }
    public DateTimeOffset ScheduledAt { get; set; }
    public DateTimeOffset ScheduledEndAt { get; set; }
    public ServiceJobStatus Status { get; set; }
    public int Revision { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public List<ServiceJobHistory> History { get; set; } = [];
    public List<ServiceDeviceReplacement> Replacements { get; set; } = [];
}
public sealed class ServiceJobHistory
{
    public int Id { get; set; }
    public int JobId { get; set; }
    public ServiceJob Job { get; set; } = null!;
    public ServiceJobAction Action { get; set; }
    public int UserId { get; set; }
    public int TechnicianId { get; set; }
    public DateTimeOffset ScheduledAt { get; set; }
    public DateTimeOffset ScheduledEndAt { get; set; }
    public string Note { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}
