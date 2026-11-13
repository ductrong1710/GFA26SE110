namespace FarmMonitoring.Domain.Entities;

public enum ConsultationStatus { NEW, IN_PROGRESS, COMPLETED, CANCELLED }
public enum ConsultationAction { SUBMITTED, CLAIMED, FOLLOW_UP, SURVEY_RECORDED, CUSTOMER_BOUND, PROPOSAL_CREATED, PLAN_CONFIRMED, DEPOSIT_CONFIRMED, INSTALLATION_SUBMITTED, INSTALLATION_APPROVED, INSTALLATION_RETURNED, CHANNEL_CHANGED }

public sealed class Consultation
{
    public int Id { get; set; }
    public string? Channel { get; set; }
    public string CustomerName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string? Email { get; set; }
    public string FarmLocation { get; set; } = "";
    public decimal? FarmArea { get; set; }
    public string ServiceNeed { get; set; } = "";
    public string Content { get; set; } = "";
    public ConsultationStatus Status { get; set; }
    public int? EngineerId { get; set; }
    public User? Engineer { get; set; }
    public int? OwnerId { get; set; }
    public User? Owner { get; set; }
    public int? FarmId { get; set; }
    public Farm? Farm { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public List<ConsultationHistory> History { get; set; } = [];
}

public sealed class ConsultationHistory
{
    public int Id { get; set; }
    public int ConsultationId { get; set; }
    public Consultation Consultation { get; set; } = null!;
    public int? UserId { get; set; }
    public User? User { get; set; }
    public ConsultationAction Action { get; set; }
    public ConsultationStatus Status { get; set; }
    public string? Notes { get; set; }
    public string? ContactResult { get; set; }
    public string? NextAction { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
