using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Application.Features.Alerts;
using FarmMonitoring.Infrastructure.BackgroundJobs;
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
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, bool development = false)
    {
        services.AddOptions<DatabaseOptions>()
            .Configure(options => options.ConnectionString = configuration.GetConnectionString("DefaultConnection") ?? "")
            .Validate(options => options.IsValid(), "ConnectionStrings:DefaultConnection requires a valid PostgreSQL connection with Host and Database.")
            .ValidateOnStart();

        services.AddOptions<JwtOptions>().Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(x => x.IsValid(development), "Jwt requires issuer, audience, an external key of at least 32 UTF-8 bytes (sample key allowed only in Development), access lifetime 1-60 minutes and refresh lifetime 1-90 days.")
            .ValidateOnStart();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IDeviceAuthenticator, DeviceAuthenticator>();
        services.AddDbContext<AppDbContext>((provider, options) =>
            options.UseNpgsql(provider.GetRequiredService<IOptions<DatabaseOptions>>().Value.ConnectionString));
        services.AddScoped<IAuthRepository, AuthRepository>();
        services.AddScoped<IPasswordRecoveryRepository, PasswordRecoveryRepository>();
        services.AddSingleton<PasswordResetQueue>();
        services.AddSingleton<IPasswordResetQueue>(provider => provider.GetRequiredService<PasswordResetQueue>());
        services.AddHostedService<PasswordResetWorker>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IFarmAccessRepository, FarmAccessRepository>();
        services.AddScoped<IFarmRepository, FarmRepository>();
        services.AddScoped<ISensorRepository, SensorRepository>();
        services.AddScoped<IEquipmentRepository, EquipmentRepository>();
        services.AddScoped<IGatewayCredentialRepository, GatewayCredentialRepository>();
        services.AddScoped<IThresholdRepository, ThresholdRepository>();
        services.AddScoped<IMissionRepository, MissionRepository>();
        services.AddScoped<IFlightTestRepository, FlightTestRepository>();
        services.AddScoped<ITelemetryRepository, TelemetryRepository>();
        services.AddScoped<ISyncRepository, SyncRepository>();
        services.AddScoped<ISensorDataRepository, SensorDataRepository>();
        services.AddScoped<IAlertRepository, AlertRepository>();
        services.AddScoped<IReportRepository, ReportRepository>();
        services.AddScoped<IAlertMonitoringRepository, AlertMonitoringRepository>();
        services.AddOptions<MonitoringSettings>().Bind(configuration.GetSection("Monitoring"))
            .Validate(x => x.IntervalSeconds is >= 1 and <= 86400 && x.GatewayOfflineMinutes is >= 1 and <= 525600 && x.UavLowBatteryPercent is >= 0 and <= 100,
                "Monitoring interval, gateway timeout and UAV battery threshold are out of range.").ValidateOnStart();
        services.AddSingleton(provider => provider.GetRequiredService<IOptions<MonitoringSettings>>().Value);
        services.AddHostedService<AlertMonitoringWorker>();
        services.AddOptions<EmailDeliverySettings>().Bind(configuration.GetSection("Email"))
            .Validate(x => x.IntervalSeconds is >= 1 and <= 86400 && x.BatchSize is >= 1 and <= 1000 && x.TimeoutSeconds is >= 1 and <= 300,
                "Email interval, batch size and timeout are out of range.").ValidateOnStart();
        services.AddSingleton(provider => provider.GetRequiredService<IOptions<EmailDeliverySettings>>().Value);
        services.AddOptions<SmtpOptions>().Bind(configuration.GetSection("Email"))
            .Validate(x => x.IsValid(), "Enabled email requires SMTP host, port, sender address and paired credentials.").ValidateOnStart();
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddScoped<IEmailDeliveryRepository, EmailDeliveryRepository>();
        services.AddScoped<EmailDeliveryService>();
        services.AddHostedService<EmailDeliveryWorker>();
        services.AddSingleton<IPasswordService, PasswordService>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddScoped<DevelopmentAdminSeeder>();
        return services;
    }
}
