using System.Text;

namespace FarmMonitoring.Infrastructure.Authentication;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Issuer { get; set; } = "";
    public string Audience { get; set; } = "";
    public string SecretKey { get; set; } = "";
    public int AccessTokenExpirationMinutes { get; set; } = 15;
    public int RefreshTokenExpirationDays { get; set; } = 7;

    public bool IsValid() => !string.IsNullOrWhiteSpace(Issuer) && !string.IsNullOrWhiteSpace(Audience)
        && !string.IsNullOrWhiteSpace(SecretKey) && Encoding.UTF8.GetByteCount(SecretKey) >= 32
        && AccessTokenExpirationMinutes is >= 1 and <= 60
        && RefreshTokenExpirationDays is >= 1 and <= 90;
}
