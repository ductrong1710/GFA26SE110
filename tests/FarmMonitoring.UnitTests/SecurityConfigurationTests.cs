using FarmMonitoring.Infrastructure;
using FarmMonitoring.Infrastructure.Authentication;
using FarmMonitoring.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FarmMonitoring.UnitTests;

public class SecurityConfigurationTests
{
    [Theory]
    [InlineData("Jwt:SecretKey", "short")]
    [InlineData("Jwt:Issuer", "")]
    [InlineData("Jwt:Audience", "")]
    [InlineData("Jwt:AccessTokenExpirationMinutes", "0")]
    [InlineData("Jwt:AccessTokenExpirationMinutes", "61")]
    [InlineData("Jwt:RefreshTokenExpirationDays", "0")]
    public void Invalid_jwt_configuration_fails_validation(string field, string value)
    {
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=tests",
            ["Jwt:Issuer"] = "test", ["Jwt:Audience"] = "test",
            ["Jwt:SecretKey"] = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
        };
        values[field] = value;
        using var services = new ServiceCollection().AddInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(values).Build()).BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() => services.GetRequiredService<IOptions<JwtOptions>>().Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-connection-string")]
    [InlineData("Host=localhost")]
    public void Missing_or_invalid_database_configuration_fails_validation(string connection)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = connection
        }).Build();
        using var services = new ServiceCollection().AddInfrastructure(config).BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() => services.GetRequiredService<IOptions<DatabaseOptions>>().Value);
    }
}
