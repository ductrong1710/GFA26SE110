using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace FarmMonitoring.Infrastructure.Persistence;

// EF tooling does not start the web host or trigger development account seeding.
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
        var configuration = new ConfigurationBuilder();
        var directory = FindApiDirectory(Directory.GetCurrentDirectory()) ?? FindApiDirectory(AppContext.BaseDirectory);
        if (directory is not null)
            configuration.SetBasePath(directory)
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile($"appsettings.{environment}.json", optional: true);
        if (environment == "Development")
            // Matches UserSecretsId in FarmMonitoring.Api.csproj without starting the web host.
            configuration.AddUserSecrets("FarmMonitoring-Api-Development");
        configuration.AddEnvironmentVariables();
        var connection = configuration.Build().GetConnectionString("DefaultConnection");
        if (!new DatabaseOptions { ConnectionString = connection ?? "" }.IsValid())
            throw new InvalidOperationException("Configure ConnectionStrings:DefaultConnection with Host and Database in FarmMonitoring.Api/appsettings.json, environment-specific settings, Development user secrets, or ConnectionStrings__DefaultConnection before running EF tooling.");
        return new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
    }

    private static string? FindApiDirectory(string start)
    {
        for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
        {
            foreach (var candidate in new[] { directory.FullName, Path.Combine(directory.FullName, "src", "FarmMonitoring.Api"), Path.Combine(directory.FullName, "FarmMonitoring.Api") })
                if (File.Exists(Path.Combine(candidate, "FarmMonitoring.Api.csproj"))) return candidate;
        }
        return null;
    }
}
