namespace FarmMonitoring.Domain.Entities;

public enum RentalStatus { DRAFT, ACTIVE, EXPIRED, ENDED }
public sealed class RentalContract
{
    public int Id { get; set; }
    public int InstallationJobId { get; set; }
    public InstallationJob InstallationJob { get; set; } = null!;
    public int FarmId { get; set; }
    public int OwnerId { get; set; }
    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset EndAt { get; set; }
    public RentalStatus Status { get; set; }
    public int Revision { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? WarnedForEndAt { get; set; }
    public List<RentalDeviceAssignment> Devices { get; set; } = [];
    public List<RentalHistory> History { get; set; } = [];
    public RentalStatus EffectiveStatus(DateTimeOffset now) => Status == RentalStatus.ACTIVE && EndAt <= now ? RentalStatus.EXPIRED : Status;
}
public sealed class RentalDeviceAssignment
{
    public int Id { get; set; }
    public int ContractId { get; set; }
    public RentalContract Contract { get; set; } = null!;
    public int InstallationAllocationId { get; set; }
    public int? SensorNodeId { get; set; }
    public int? UavId { get; set; }
    public int? GatewayId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string InstallationLocation { get; set; } = "";
    public DateTimeOffset? ReturnedAt { get; set; }
}
public sealed class RentalHistory
{
    public int Id { get; set; }
    public int ContractId { get; set; }
    public RentalContract Contract { get; set; } = null!;
    public string Action { get; set; } = "";
    public int? UserId { get; set; }
    public string Note { get; set; } = "";
    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset EndAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
