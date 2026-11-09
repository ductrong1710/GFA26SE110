using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using FarmMonitoring.Domain.Entities;
using FarmMonitoring.Infrastructure.Authentication;
using Microsoft.Extensions.Options;

namespace FarmMonitoring.UnitTests;

public class JwtLifetimeTests
{
    [Fact]
    public void Advertised_expiration_matches_the_signed_token_exactly()
    {
        var service = new JwtTokenService(Options.Create(new JwtOptions
        {
            Issuer = "test", Audience = "test",
            SecretKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))
        }));
        var now = new DateTimeOffset(2026, 9, 24, 1, 0, 0, TimeSpan.Zero).AddMilliseconds(750);
        var result = service.CreateAccessToken(new User { Id = 1, Email = "test@example.com" }, now);
        var signed = new JwtSecurityTokenHandler().ReadJwtToken(result.Token);
        Assert.Equal(new DateTimeOffset(signed.ValidTo, TimeSpan.Zero), result.ExpiresAt);
        Assert.Equal(TimeSpan.FromMinutes(15), signed.ValidTo - signed.ValidFrom);
        Assert.Equal(900, result.ExpiresIn);
    }
}
