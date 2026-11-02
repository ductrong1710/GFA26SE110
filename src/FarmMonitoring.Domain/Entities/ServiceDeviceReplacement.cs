namespace FarmMonitoring.Domain.Entities;

public sealed class ServiceDeviceReplacement
{
    public int Id { get; set; }
    public int JobId { get; set; }
    public ServiceJob Job { get; set; } = null!;
    public int OldAssignmentId { get; set; }
    public RentalDeviceAssignment OldAssignment { get; set; } = null!;
    public int NewAssignmentId { get; set; }
    public RentalDeviceAssignment NewAssignment { get; set; } = null!;
    public string OldDeviceCode { get; set; } = "";
    public string NewDeviceCode { get; set; } = "";
    public int TechnicianId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string Note { get; set; } = "";
}
