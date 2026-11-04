namespace FarmMonitoring.Domain.Entities;

public sealed class MaintenanceSchedule
{
    public int Id { get; set; }
    public int? FarmId { get; set; }
    public int? RentalDeviceAssignmentId { get; set; }
    public string? DeviceType { get; set; }
    public string Policy { get; set; } = "";
    public int IntervalDays { get; set; }
    public DateTimeOffset NextDueAt { get; set; }
    public int TechnicianId { get; set; }
    public bool Enabled { get; set; } = true;
    public int Revision { get; set; } = 1;
    public int CreatedById { get; set; }
    public int UpdatedById { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class MaintenanceOccurrence
{
    public int Id { get; set; }
    public int ScheduleId { get; set; }
    public DateTimeOffset DueAt { get; set; }
    public int RentalDeviceAssignmentId { get; set; }
    public int JobId { get; set; }
    public string Policy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}
