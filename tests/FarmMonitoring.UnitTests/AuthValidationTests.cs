using FarmMonitoring.Application.Features.Auth;

namespace FarmMonitoring.UnitTests;

public class AuthValidationTests
{
    [Theory]
    [InlineData("", "password")]
    [InlineData("not-an-email", "password")]
    [InlineData("user@example.com", "")]
    [InlineData("user@example.com", "   ")]
    [InlineData(null, null)]
    public void Login_rejects_missing_or_invalid_credentials(string? email, string? password)
    {
        var result = new LoginRequestValidator().Validate(new LoginRequest(email!, password!));
        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Refresh_rejects_missing_token(string? token)
    {
        Assert.False(new RefreshRequestValidator().Validate(new RefreshRequest(token!)).IsValid);
    }

    [Fact]
    public void Login_accepts_valid_email_with_outer_whitespace()
    {
        Assert.True(new LoginRequestValidator().Validate(new LoginRequest("  ADMIN@example.com ", "valid-password")).IsValid);
    }
}
