namespace FarmMonitoring.Domain.Entities;

public sealed class SiteVisitOfficeLocation
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string MapsUrl { get; set; } = "";
}
