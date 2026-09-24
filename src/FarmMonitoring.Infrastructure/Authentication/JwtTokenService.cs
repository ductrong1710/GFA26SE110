using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FarmMonitoring.Application.Features.Auth;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FarmMonitoring.Infrastructure.Authentication;

public sealed class JwtTokenService(IOptions<JwtOptions> options) : ITokenService
{
    private readonly JwtOptions settings = options.Value;

    public AccessTokenResult CreateAccessToken(User user, DateTimeOffset now)
    {
        // JWT NumericDate uses whole seconds; expose the same expiry to clients.
        now = DateTimeOffset.FromUnixTimeSeconds(now.ToUnixTimeSeconds());
        var expires = now.AddMinutes(settings.AccessTokenExpirationMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString(CultureInfo.InvariantCulture)),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Convert.ToHexString(RandomNumberGenerator.GetBytes(16)))
        };
        claims.AddRange(user.UserRoles.Select(x => x.Role.Name).Distinct().Select(role => new Claim("role", role)));
        var token = new JwtSecurityToken(settings.Issuer, settings.Audience, claims,
            now.UtcDateTime, expires.UtcDateTime,
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SecretKey)), SecurityAlgorithms.HmacSha256));
        return new AccessTokenResult(new JwtSecurityTokenHandler().WriteToken(token), expires,
            checked(settings.AccessTokenExpirationMinutes * 60));
    }

    public RefreshTokenMaterial CreateRefreshToken(DateTimeOffset now)
    {
        var token = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(64));
        return new RefreshTokenMaterial(token, HashRefreshToken(token), now.AddDays(settings.RefreshTokenExpirationDays));
    }

    public string HashRefreshToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
