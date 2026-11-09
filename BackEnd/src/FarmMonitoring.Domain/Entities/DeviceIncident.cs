namespace FarmMonitoring.Domain.Entities;

public enum DeviceIncidentStatus { OPEN, RESOLVED }
public sealed class DeviceIncident
{
    public int Id { get; set; }
    public int FarmId { get; set; }
    public int? RentalDeviceAssignmentId { get; set; }
    public RentalDeviceAssignment? RentalDeviceAssignment { get; set; }
    public string DeviceCode { get; set; } = "";
    public int ReporterId { get; set; }
    public string Description { get; set; } = "";
    public DateTimeOffset ReportedAt { get; set; }
    public DeviceIncidentStatus Status { get; set; } = DeviceIncidentStatus.OPEN;
    public int? MissionId { get; set; }
    public List<EvidenceImage> Images { get; set; } = [];
}
