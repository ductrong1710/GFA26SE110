namespace FarmMonitoring.Application.Interfaces;

public interface IDeviceAuthenticator
{
    Task<int?> AuthenticateAsync(string gatewayCode, string apiKey, CancellationToken ct);
}
