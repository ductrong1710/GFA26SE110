namespace FarmMonitoring.Domain.Entities;

public sealed class User
{
    public ICollection<UserFarm> UserFarms { get; set; } = new List<UserFarm>();
    public int Id { get; set; }
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string FullName { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
