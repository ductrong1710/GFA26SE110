namespace FarmMonitoring.Domain.Entities;

public sealed class Role
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
}
