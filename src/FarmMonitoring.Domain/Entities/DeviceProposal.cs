namespace FarmMonitoring.Domain.Entities;

public enum ProposalStatus { DRAFT, PLAN_CONFIRMED, DEPOSIT_CONFIRMED, SUBMITTED }
public sealed class DeviceProposal
{
    public int Id { get; set; }
    public int ConsultationId { get; set; }
    public Consultation Consultation { get; set; } = null!;
    public int SurveyId { get; set; }
    public FarmSurvey Survey { get; set; } = null!;
    public int Revision { get; set; }
    public int CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public int RentalMonths { get; set; }
    public DateTimeOffset RequestedInstallationAt { get; set; }
    public string? Notes { get; set; }
    public ProposalStatus Status { get; set; }
    public int? PlanConfirmedByUserId { get; set; }
    public DateTimeOffset? PlanConfirmedAt { get; set; }
    public string? PlanConfirmationNote { get; set; }
    public int? DepositConfirmedByUserId { get; set; }
    public DateTimeOffset? DepositConfirmedAt { get; set; }
    public string? DepositConfirmationNote { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public List<ProposalLine> Lines { get; set; } = [];
}

public sealed class ProposalLine
{
    public int Id { get; set; }
    public int ProposalId { get; set; }
    public DeviceProposal Proposal { get; set; } = null!;
    public int SurveyZoneId { get; set; }
    public SurveyZone SurveyZone { get; set; } = null!;
    public string DeviceType { get; set; } = "";
    public int Quantity { get; set; }
    public string InstallationLocation { get; set; } = "";
}

public enum InstallationRequestStatus { SUBMITTED, APPROVED, RETURNED }
public sealed class InstallationRequest
{
    public int Id { get; set; }
    public int ProposalId { get; set; }
    public DeviceProposal Proposal { get; set; } = null!;
    public int FarmId { get; set; }
    public Farm Farm { get; set; } = null!;
    public int OwnerId { get; set; }
    public User Owner { get; set; } = null!;
    public int SubmittedByUserId { get; set; }
    public User SubmittedByUser { get; set; } = null!;
    public InstallationRequestStatus Status { get; set; }
    public DateTimeOffset SubmittedAt { get; set; }
    public int? ReviewedByUserId { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? ReviewNote { get; set; }
}
