using System.Security.Cryptography;
using System.Text;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Entities;

namespace FarmMonitoring.Application.Features.Equipment;

public sealed class GatewayCredentialService(IGatewayCredentialRepository repository, ICurrentUser actor, TimeProvider clock)
{
    public Task<GatewayCredentialStatus> GetStatusAsync(int gatewayId, CancellationToken ct) => repository.GetStatusAsync(gatewayId, ct);

    public async Task<IssuedGatewayCredential> IssueAsync(int gatewayId, bool rotate, CancellationToken ct)
    {
        // 256 bits of entropy; URL-safe text is convenient for physical device configuration.
        var rawKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var credential = new GatewayCredential
        {
            GatewayId = gatewayId,
            KeyHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawKey))),
            KeyPrefix = rawKey[..8], CreatedAt = clock.GetUtcNow(), CreatedByUserId = actor.UserId
        };
        var gatewayCode = await repository.IssueAsync(gatewayId, credential, rotate, ct);
        return new(gatewayId, gatewayCode, rawKey, credential.CreatedAt);
    }

    public Task RevokeAsync(int gatewayId, CancellationToken ct) => repository.RevokeAsync(gatewayId, clock.GetUtcNow(), ct);
}
