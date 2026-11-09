using FarmMonitoring.Domain.Entities;

namespace FarmMonitoring.Application.Interfaces;

public interface IAuthRepository
{
    Task<User?> GetUserByEmailAsync(string normalizedEmail, CancellationToken ct);
    Task<User?> GetUserByIdAsync(int id, CancellationToken ct);
    Task SaveLoginAsync(RefreshToken token, CancellationToken ct);
    // Validation and replacement must be atomic across concurrent requests.
    Task<User?> RotateAsync(string oldHash, RefreshToken replacement, CancellationToken ct);
    Task<bool> RevokeAsync(int userId, string tokenHash, CancellationToken ct);
}
