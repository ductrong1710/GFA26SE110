using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Infrastructure.Authentication;
using FarmMonitoring.Infrastructure.Persistence;
using FarmMonitoring.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FarmMonitoring.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DatabaseOptions>()
            .Configure(options => options.ConnectionString = configuration.GetConnectionString("DefaultConnection") ?? "")
            .Validate(options => options.IsValid(), "ConnectionStrings:DefaultConnection requires a valid PostgreSQL connection with Host and Database.")
            .ValidateOnStart();

        services.AddOptions<JwtOptions>().Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(x => x.IsValid(), "Jwt requires issuer, audience, a key of at least 32 UTF-8 bytes, access lifetime 1-60 minutes and refresh lifetime 1-90 days.")
            .ValidateOnStart();
        services.AddSingleton(TimeProvider.System);
        services.AddDbContext<AppDbContext>((provider, options) =>
            options.UseNpgsql(provider.GetRequiredService<IOptions<DatabaseOptions>>().Value.ConnectionString));
        services.AddScoped<IAuthRepository, AuthRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IFarmRepository, FarmRepository>();
        services.AddScoped<ISensorRepository, SensorRepository>();
        services.AddSingleton<IPasswordService, PasswordService>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddScoped<DevelopmentAdminSeeder>();
        return services;
    }
}
