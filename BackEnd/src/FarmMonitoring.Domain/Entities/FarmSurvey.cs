namespace FarmMonitoring.Domain.Entities;

public sealed class FarmSurvey
{
    public int Id { get; set; }
    public int ConsultationId { get; set; }
    public Consultation Consultation { get; set; } = null!;
    public int Revision { get; set; }
    public int EngineerId { get; set; }
    public User Engineer { get; set; } = null!;
    public string FarmName { get; set; } = "";
    public string FarmLocation { get; set; } = "";
    public decimal? FarmArea { get; set; }
    public string MonitoringNeeds { get; set; } = "";
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public List<SurveyZone> Zones { get; set; } = [];
    public List<EvidenceImage> Images { get; set; } = [];
}

public sealed class SurveyZone
{
    public int Id { get; set; }
    public int SurveyId { get; set; }
    public FarmSurvey Survey { get; set; } = null!;
    public string Name { get; set; } = "";
    public string? Crop { get; set; }
    public int? CropAgeDays { get; set; }
    public string? Symptoms { get; set; }
    public string InstallationLocation { get; set; } = "";
}
