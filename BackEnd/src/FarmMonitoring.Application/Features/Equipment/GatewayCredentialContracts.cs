namespace FarmMonitoring.Application.Features.Equipment;

public sealed record GatewayCredentialStatus(int GatewayId, string GatewayCode, bool HasCredential, bool IsActive,
    DateTimeOffset? CreatedAt, DateTimeOffset? LastUsedAt, DateTimeOffset? RevokedAt, string? KeyPrefix);

public sealed record IssuedGatewayCredential(int GatewayId, string GatewayCode, string ApiKey, DateTimeOffset CreatedAt);
