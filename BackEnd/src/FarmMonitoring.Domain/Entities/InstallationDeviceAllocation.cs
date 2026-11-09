namespace FarmMonitoring.Domain.Entities;
public sealed class InstallationDeviceAllocation
{
    public int Id { get; set; }
    public int JobId { get; set; }
    public int ProposalLineId { get; set; }
    public int? SensorNodeId { get; set; }
    public int? UavId { get; set; }
    public int? GatewayId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public int AllocatedByUserId { get; set; }
    public DateTimeOffset AllocatedAt { get; set; }
    public DateTimeOffset? ReleasedAt { get; set; }
}
