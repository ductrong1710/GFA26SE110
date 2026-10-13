using FarmMonitoring.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FarmMonitoring.UnitTests;

[CollectionDefinition("Design-time configuration", DisableParallelization = true)]
public sealed class DesignTimeConfigurationCollection;

[Collection("Design-time configuration")]
public sealed class AppDbContextFactoryTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "farm-ef-" + Guid.NewGuid().ToString("N"));
    private readonly string previousDirectory = Directory.GetCurrentDirectory();
    private readonly Dictionary<string, string?> previous = new();
    private string ApiDirectory => Path.Combine(root, "src", "FarmMonitoring.Api");

    public AppDbContextFactoryTests()
    {
        foreach (var name in new[] { "ConnectionStrings__DefaultConnection", "DOTNET_ENVIRONMENT", "ASPNETCORE_ENVIRONMENT" })
        {
            previous[name] = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, null);
        }
        Directory.CreateDirectory(ApiDirectory);
        File.WriteAllText(Path.Combine(ApiDirectory, "FarmMonitoring.Api.csproj"), "<Project />");
        Directory.SetCurrentDirectory(root);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Reads_api_settings_from_repository_or_project_directory(bool insideApi)
    {
        Settings("appsettings.json", "base_db");
        if (insideApi) Directory.SetCurrentDirectory(ApiDirectory);
        using var db = new AppDbContextFactory().CreateDbContext([]);
        Assert.Contains("Database=base_db", db.Database.GetConnectionString());
    }

    [Fact]
    public void Environment_specific_settings_override_base_settings()
    {
        Settings("appsettings.json", "base_db");
        Settings("appsettings.Testing.json", "test_db");
        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Testing");
        using var db = new AppDbContextFactory().CreateDbContext([]);
        Assert.Contains("Database=test_db", db.Database.GetConnectionString());
    }

    [Fact]
    public void Environment_connection_remains_highest_priority()
    {
        Settings("appsettings.json", "base_db");
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", "Host=localhost;Database=override_db");
        using var db = new AppDbContextFactory().CreateDbContext([]);
        Assert.Contains("Database=override_db", db.Database.GetConnectionString());
    }

    [Fact]
    public void Missing_configuration_has_actionable_error()
    {
        var error = Assert.Throws<InvalidOperationException>(() => new AppDbContextFactory().CreateDbContext([]));
        Assert.Contains("appsettings.json", error.Message);
        Assert.Contains("ConnectionStrings__DefaultConnection", error.Message);
    }

    [Fact]
    public void Invalid_connection_error_does_not_echo_credentials()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", "Password=do-not-echo");
        var error = Assert.Throws<InvalidOperationException>(() => new AppDbContextFactory().CreateDbContext([]));
        Assert.DoesNotContain("do-not-echo", error.ToString());
    }

    private void Settings(string name, string database) => File.WriteAllText(Path.Combine(ApiDirectory, name),
        System.Text.Json.JsonSerializer.Serialize(new { ConnectionStrings = new { DefaultConnection = $"Host=localhost;Database={database}" } }));

    public void Dispose()
    {
        Directory.SetCurrentDirectory(previousDirectory);
        foreach (var item in previous) Environment.SetEnvironmentVariable(item.Key, item.Value);
        Directory.Delete(root, recursive: true);
    }
}
