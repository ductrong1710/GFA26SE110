namespace FarmMonitoring.Domain.Entities;

public enum ServicePlanStatus { PROPOSED, APPROVED, REJECTED, EXECUTED, APPLIED }
public sealed class ServicePlan
{
    public int Id { get; set; }
    public int JobId { get; set; }
    public ServiceJob Job { get; set; } = null!;
    public int OldAssignmentId { get; set; }
    public int? SensorNodeId { get; set; }
    public int? UavId { get; set; }
    public int? GatewayId { get; set; }
    public int TechnicianId { get; set; }
    public int? ReviewerId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string Note { get; set; } = "";
    public string? ReviewNote { get; set; }
    public ServicePlanStatus Status { get; set; }
    public string PriceLinesJson { get; set; } = "[]";
    public decimal TotalVnd { get; set; }
    public string EvidenceJson { get; set; } = "{}";
}
