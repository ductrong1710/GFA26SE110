using FarmMonitoring.Application.Features.Equipment;
using FarmMonitoring.Domain.Entities;

namespace FarmMonitoring.Application.Interfaces;

public interface IGatewayCredentialRepository
{
    Task<GatewayCredentialStatus> GetStatusAsync(int gatewayId, CancellationToken ct);
    Task<string> IssueAsync(int gatewayId, GatewayCredential credential, bool rotate, CancellationToken ct);
    Task RevokeAsync(int gatewayId, DateTimeOffset revokedAt, CancellationToken ct);
}
