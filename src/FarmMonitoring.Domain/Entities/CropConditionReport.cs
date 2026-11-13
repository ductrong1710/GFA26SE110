namespace FarmMonitoring.Domain.Entities;

public enum CropConditionStatus { NEW, IN_REVIEW, AWAITING_INFORMATION, RESOLVED }
public enum CropConsultationAction { SUBMITTED, CLAIMED, INFORMATION_REQUESTED, OWNER_REPLIED, RECOMMENDED, RESOLVED, ASSIGNED, FOLLOW_UP, REVIEW_REQUESTED, REOPENED, REFERRED }
public sealed class CropConditionReport
{
    public int Id { get; set; }
    public int FarmId { get; set; }
    public int? ZoneId { get; set; }
    public int? CropSeasonId { get; set; }
    public DateTimeOffset? ObservedAt { get; set; }
    public int ReporterId { get; set; }
    public int? EngineerId { get; set; }
    public string Crop { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTimeOffset ReportedAt { get; set; }
    public CropConditionStatus Status { get; set; } = CropConditionStatus.NEW;
    public int Revision { get; set; } = 1;
    public List<CropConsultationUpdate> Updates { get; set; } = [];
    public List<CropConditionSensor> Sensors { get; set; } = [];
    public List<EvidenceImage> Images { get; set; } = [];
}
public sealed class CropConsultationUpdate
{
    public int Id { get; set; }
    public int ReportId { get; set; }
    public CropConditionReport Report { get; set; } = null!;
    public int UserId { get; set; }
    public CropConsultationAction Action { get; set; }
    public string Content { get; set; } = "";
    public bool RecommendSiteSurvey { get; set; }
    public string? FollowUpType { get; set; }
    public int? CultivationActivityId { get; set; }
    public int? AssignedEngineerId { get; set; }
    public string? ReferralType { get; set; }
    public int? DeviceIncidentId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public List<EvidenceImage> Images { get; set; } = [];
}
public sealed class CropConditionSensor
{
    public int ReportId { get; set; }
    public CropConditionReport Report { get; set; } = null!;
    public int SensorChannelId { get; set; }
}
