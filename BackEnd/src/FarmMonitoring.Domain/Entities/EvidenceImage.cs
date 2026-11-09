namespace FarmMonitoring.Domain.Entities;

public sealed class EvidenceImage
{
    public int Id { get; set; }
    public int? CultivationActivityId { get; set; }
    public int? CropConditionReportId { get; set; }
    public int? CropConsultationUpdateId { get; set; }
    public int? IncidentId { get; set; }
    public int? SurveyId { get; set; }
    public int? ReportId { get; set; }
    public int? ServiceReportId { get; set; }
    public int? MissionLogId { get; set; }
    public string ContentType { get; set; } = "";
    public byte[] Content { get; set; } = [];
}
