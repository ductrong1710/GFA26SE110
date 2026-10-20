using FarmMonitoring.Domain.Entities;

namespace FarmMonitoring.Application.Interfaces;

public interface IPasswordRecoveryRepository
{
    Task<bool> IssueAsync(User user, string hash, CancellationToken ct);
    Task<User?> FindResetAsync(string hash, CancellationToken ct);
    Task<bool> ReplaceAsync(User user, string passwordHash, string? resetHash, CancellationToken ct);
}
