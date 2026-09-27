using System.Security.Cryptography;
using System.Text;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FarmMonitoring.Infrastructure.Authentication;

public sealed class DeviceAuthenticationOptions
{
    public List<DeviceCredential> Credentials { get; set; } = [];
    public bool IsValid() => Credentials.All(x => !string.IsNullOrWhiteSpace(x.GatewayCode) && x.GatewayCode.Length <= 100
        && x.KeyHash.Length == 64 && x.KeyHash.All(Uri.IsHexDigit))
        && Credentials.Select(x => x.GatewayCode).Distinct(StringComparer.Ordinal).Count() == Credentials.Count;
}
public sealed class DeviceCredential
{
    public string GatewayCode { get; set; } = "";
    public string KeyHash { get; set; } = "";
}
public sealed class DeviceAuthenticator(AppDbContext db, IOptions<DeviceAuthenticationOptions> configured) : IDeviceAuthenticator
{
    public async Task<int?> AuthenticateAsync(string gatewayCode, string apiKey, CancellationToken ct)
    {
        if (gatewayCode.Length is 0 or > 100 || apiKey.Length is < 32 or > 256) return null;
        var credential = configured.Value.Credentials.SingleOrDefault(x => x.GatewayCode == gatewayCode);
        if (credential is null) return null;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(apiKey));
        if (!CryptographicOperations.FixedTimeEquals(hash, Convert.FromHexString(credential.KeyHash))) return null;
        return await db.Gateways.AsNoTracking().Where(x => x.Code == gatewayCode && x.IsActive).Select(x => (int?)x.Id).SingleOrDefaultAsync(ct);
    }
}
