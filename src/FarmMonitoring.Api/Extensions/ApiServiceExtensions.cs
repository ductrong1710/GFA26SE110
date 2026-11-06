using FarmMonitoring.Application.Features.PublicInformation;
using System.Text;
using FarmMonitoring.Application.Features.CropSupport;
using FarmMonitoring.Application.Features.CropSeasons;
using FarmMonitoring.Application.Features.TechnicalSupport;
using FarmMonitoring.Application.Features.Rentals;
using FarmMonitoring.Api.Authorization;
using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Users;
using FarmMonitoring.Application.Features.Farms;
using FarmMonitoring.Application.Features.Sensors;
using FarmMonitoring.Application.Features.Equipment;
using FarmMonitoring.Application.Features.Thresholds;
using FarmMonitoring.Application.Features.Missions;
using FarmMonitoring.Application.Features.Telemetry;
using FarmMonitoring.Application.Features.Sync;
using FarmMonitoring.Application.Features.SensorData;
using FarmMonitoring.Application.Features.Alerts;
using FarmMonitoring.Application.Features.Reports;
using Microsoft.AspNetCore.Authentication;
using FarmMonitoring.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using FarmMonitoring.Api.Contracts;
using FarmMonitoring.Api.Filters;
using FarmMonitoring.Application.Features.Auth;
using FarmMonitoring.Infrastructure.Authentication;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

namespace FarmMonitoring.Api.Extensions;

public static class ApiServiceExtensions
{
    public static IServiceCollection AddApiServices(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        services.AddScoped<RentalService>();
        services.AddScoped<DeviceIncidentService>();
        services.AddScoped<PublicContentService>();
        services.AddScoped<CropSupportService>();
        services.AddScoped<CropSeasonService>();
        services.AddScoped<IValidator<CropSeasonRequest>, CropSeasonValidator>();
        services.AddScoped<IValidator<CompleteCropSeasonRequest>, CompleteCropSeasonValidator>();
        services.AddScoped<IValidator<CultivationActivityRequest>, CultivationActivityValidator>();
        services.AddScoped<IValidator<CreateCropConditionRequest>, CreateCropConditionValidator>();
        services.AddScoped<IValidator<CropConsultationRequest>, CropConsultationValidator>();
        services.AddScoped<ServiceJobService>();
        services.AddScoped<MaintenanceScheduleService>();
        services.AddScoped<IValidator<MaintenanceScheduleRequest>, MaintenanceScheduleValidator>();
        services.AddScoped<ServiceReportService>();
        services.AddScoped<ServiceReplacementService>();
        services.AddScoped<IValidator<ServiceReplacementRequest>, ServiceReplacementValidator>();
        services.AddScoped<IValidator<ServiceReportRequest>, ServiceReportValidator>();
        services.AddScoped<IValidator<ServiceJobAssignmentRequest>, ServiceJobAssignmentValidator>();
        services.AddScoped<IValidator<CreateDeviceIncidentRequest>, CreateDeviceIncidentValidator>();
        services.AddScoped<IValidator<CreateRentalRequest>, CreateRentalValidator>();
        services.AddScoped<IValidator<RentalActionRequest>, RentalActionValidator>();
        services.AddScoped<IValidator<ExtendRentalRequest>, ExtendRentalValidator>();
        services.AddScoped<IValidator<EndRentalRequest>, EndRentalValidator>();
        services.AddScoped<PasswordRecoveryService>();
        services.AddScoped<UserService>();
        services.AddHttpContextAccessor();
        services.AddScoped<FarmMonitoring.Application.Interfaces.ICurrentUser, CurrentUser>();
        services.AddScoped<FarmAccessService>();
        services.AddScoped<FarmMonitoring.Application.Features.Installations.InstallationReviewService>();
        services.AddScoped<FarmMonitoring.Application.Features.Installations.InstallationJobService>();
        services.AddScoped<FarmMonitoring.Application.Features.Installations.InstallationReportService>();
        services.AddScoped<FarmMonitoring.Application.Features.Installations.InstallationDeviceService>();
        services.AddScoped<IValidator<FarmMonitoring.Application.Features.Installations.JobDevicesRequest>, FarmMonitoring.Application.Features.Installations.JobDevicesValidator>();
        services.AddScoped<IValidator<FarmMonitoring.Application.Features.Installations.InstallationReportRequest>, FarmMonitoring.Application.Features.Installations.InstallationReportValidator>();
        services.AddScoped<IValidator<FarmMonitoring.Application.Features.Installations.JobAssignmentRequest>, FarmMonitoring.Application.Features.Installations.JobAssignmentValidator>();
        services.AddScoped<IValidator<FarmMonitoring.Application.Features.Installations.InstallationReviewRequest>, FarmMonitoring.Application.Features.Installations.InstallationReviewValidator>();
        services.AddScoped<FarmMonitoring.Application.Features.Consultations.ConsultationService>();
        services.AddScoped<FarmMonitoring.Application.Features.Consultations.ProposalService>();
        services.AddScoped<IValidator<FarmMonitoring.Application.Features.Consultations.ProposalRequest>, FarmMonitoring.Application.Features.Consultations.ProposalValidator>();
        services.AddScoped<IValidator<FarmMonitoring.Application.Features.Consultations.ProposalConfirmationRequest>, FarmMonitoring.Application.Features.Consultations.ProposalConfirmationValidator>();
        services.AddScoped<IValidator<FarmMonitoring.Application.Features.Consultations.SurveyRequest>, FarmMonitoring.Application.Features.Consultations.SurveyValidator>();
        services.AddScoped<IValidator<FarmMonitoring.Application.Features.Consultations.SubmitConsultationRequest>, FarmMonitoring.Application.Features.Consultations.SubmitConsultationValidator>();
        services.AddScoped<IValidator<FarmMonitoring.Application.Features.Consultations.FollowUpRequest>, FarmMonitoring.Application.Features.Consultations.FollowUpValidator>();
        services.AddScoped<FarmMembershipService>();
        services.AddScoped<FarmService>();
        services.AddScoped<SensorService>();
        services.AddScoped<EquipmentService>();
        services.AddScoped<GatewayCredentialService>();
        services.AddScoped<ThresholdService>();
        services.AddScoped<MissionService>();
        services.AddScoped<MissionLaunchService>();
        services.AddScoped<MissionLifecycle>();
        services.AddScoped<DeviceMissionService>();
        services.AddScoped<FarmMonitoring.Application.Features.FlightTests.FlightTestService>();
        services.AddScoped<TelemetryService>();
        services.AddScoped<SyncService>();
        services.AddScoped<AlertService>();
        services.AddScoped<ReportService>();
        services.AddScoped<AlertMonitoringService>();
        services.AddScoped<IValidator<AlertQuery>, AlertQueryValidator>();
        services.AddScoped<SensorDataService>();
        services.AddScoped<IValidator<SyncRequest>, SyncValidator>();
        services.AddScoped<IValidator<ReadingQuery>, ReadingQueryValidator>();
        services.AddScoped<IValidator<TelemetryRequest>, TelemetryValidator>();
        services.AddScoped<IValidator<TelemetryQuery>, TelemetryQueryValidator>();
        services.AddScoped<IValidator<MissionRequest>, MissionValidator>();
        services.AddScoped<IValidator<ScheduleMissionRequest>, ScheduleMissionValidator>();
        services.AddScoped<IValidator<MissionActionRequest>, MissionActionValidator>();
        services.AddScoped<IValidator<MissionQuery>, MissionQueryValidator>();
        services.AddScoped<IValidator<ThresholdRequest>, ThresholdValidator>();
        services.AddScoped<IValidator<UavRequest>, UavValidator>();
        services.AddScoped<IValidator<GatewayRequest>, GatewayValidator>();
        services.AddScoped<IValidator<EquipmentStatusRequest>, EquipmentStatusValidator>();
        services.AddScoped<IValidator<AssignUavRequest>, AssignUavValidator>();
        services.AddScoped<IValidator<SensorTypeRequest>, SensorTypeValidator>();
        services.AddScoped<IValidator<SensorNodeRequest>, SensorNodeValidator>();
        services.AddScoped<IValidator<SensorNodeStatusRequest>, SensorNodeStatusValidator>();
        services.AddScoped<IValidator<SensorChannelRequest>, SensorChannelValidator>();
        services.AddScoped<IValidator<FarmRequest>, FarmValidator>();
        services.AddScoped<IValidator<ZoneRequest>, ZoneValidator>();
        services.AddScoped<IValidator<FarmStatusRequest>, FarmStatusValidator>();
        services.AddScoped<IValidator<CreateUserRequest>, CreateUserValidator>();
        services.AddScoped<IValidator<UpdateUserRequest>, UpdateUserValidator>();
        services.AddScoped<IValidator<UserStatusRequest>, UserStatusValidator>();
        services.AddScoped<IValidator<UserRolesRequest>, UserRolesValidator>();
        services.AddScoped<IValidator<PageQuery>, PageQueryValidator>();
        services.AddScoped<IValidator<LoginRequest>, LoginRequestValidator>();
        services.AddScoped<IValidator<RefreshRequest>, RefreshRequestValidator>();
        services.AddControllers(options => options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true);
        services.Configure<ApiBehaviorOptions>(options => options.InvalidModelStateResponseFactory = context =>
            new BadRequestObjectResult(ApiError.Create(context.HttpContext, "Validation failed.",
                context.ModelState.Where(x => x.Value?.Errors.Count > 0)
                    .Select(x => new ApiFieldError(x.Key, "Invalid request value.")).ToArray())));

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer()
            .AddScheme<AuthenticationSchemeOptions, DeviceAuthenticationHandler>(DeviceAuthenticationHandler.SchemeName, _ => { });
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((options, configured) =>
            {
                var jwt = configured.Value;
                options.MapInboundClaims = false;
                options.IncludeErrorDetails = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true, ValidIssuer = jwt.Issuer,
                    ValidateAudience = true, ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SecretKey)),
                    ValidateLifetime = true, RequireExpirationTime = true, RequireSignedTokens = true,
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ClockSkew = TimeSpan.Zero, NameClaimType = "sub", RoleClaimType = "role"
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        if (!int.TryParse(context.Principal?.FindFirst("sub")?.Value, out var userId))
                        { context.Fail("Invalid subject."); return; }
                        var versionClaim = context.Principal?.FindFirst("session_version")?.Value;
                        var version = 0; // Tokens issued before this migration belong to version zero.
                        if (versionClaim is not null && !int.TryParse(versionClaim, out version))
                        { context.Fail("Invalid session version."); return; }
                        var repository = context.HttpContext.RequestServices.GetRequiredService<FarmMonitoring.Application.Interfaces.IAuthRepository>();
                        var user = await repository.GetUserByIdAsync(userId, context.HttpContext.RequestAborted);
                        if (user is null || user.PasswordVersion != version) context.Fail("Session revoked.");
                    },
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.Headers.WWWAuthenticate = "Bearer";
                        await context.Response.WriteAsJsonAsync(ApiError.Create(context.HttpContext, "Authentication required."));
                    },
                    OnForbidden = async context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        await context.Response.WriteAsJsonAsync(ApiError.Create(context.HttpContext, "You do not have permission to perform this action."));
                    }
                };
            });
        services.AddScoped<IAuthorizationHandler, CurrentRoleHandler>();
        services.AddAuthorization(options =>
        {
            options.AddPolicy(AccessPolicies.ManageMaintenanceSchedules, policy => policy.RequireAuthenticatedUser()
                .AddRequirements(new CurrentRoleRequirement(RoleNames.FarmAdministrator)));
            options.AddPolicy(AccessPolicies.OperationalUser, policy => policy.RequireAuthenticatedUser()
                .AddRequirements(new CurrentRoleRequirement(RoleNames.FarmAdministrator, RoleNames.FarmOwner, RoleNames.FarmEngineer, RoleNames.Technician)));
            options.AddPolicy(AccessPolicies.ReviewInstallations, policy => policy.RequireAuthenticatedUser()
                .AddRequirements(new CurrentRoleRequirement(RoleNames.FarmAdministrator)));
            options.AddPolicy(AccessPolicies.ManageUsers, policy => policy.RequireAuthenticatedUser()
                .AddRequirements(new CurrentRoleRequirement(RoleNames.FarmAdministrator)));
            options.AddPolicy(AccessPolicies.ManageFarms, policy => policy.RequireAuthenticatedUser()
                .AddRequirements(new CurrentRoleRequirement(RoleNames.FarmAdministrator)));
            options.AddPolicy(AccessPolicies.ReadFarmData, policy => policy.RequireAuthenticatedUser()
                .AddRequirements(new CurrentRoleRequirement(RoleNames.FarmAdministrator, RoleNames.FarmOwner)));
            options.AddPolicy(AccessPolicies.CreateDevices, policy => policy.RequireAuthenticatedUser()
                .AddRequirements(new CurrentRoleRequirement(RoleNames.FarmAdministrator)));
            options.AddPolicy(AccessPolicies.ManageDevices, policy => policy.RequireAuthenticatedUser()
                .AddRequirements(new CurrentRoleRequirement(RoleNames.FarmAdministrator)));
            options.AddPolicy(AccessPolicies.ConfigureThresholds, policy => policy.RequireAuthenticatedUser()
                .AddRequirements(new CurrentRoleRequirement(RoleNames.FarmAdministrator)));
            options.AddPolicy(AccessPolicies.SubmitDeviceIncidents, policy => policy.RequireAuthenticatedUser()
                .AddRequirements(new CurrentRoleRequirement(RoleNames.FarmOwner)));
            options.AddPolicy(AccessPolicies.ManageMissions, policy => policy.RequireAuthenticatedUser()
                .AddRequirements(new CurrentRoleRequirement(RoleNames.FarmOwner)));
            options.AddPolicy(AccessPolicies.ProcessConsultations, policy => policy.RequireAuthenticatedUser()
                .AddRequirements(new CurrentRoleRequirement(RoleNames.FarmEngineer)));
            options.AddPolicy(AccessPolicies.ReadConsultations, policy => policy.RequireAuthenticatedUser()
                .AddRequirements(new CurrentRoleRequirement(RoleNames.FarmEngineer, RoleNames.FarmAdministrator)));
        });
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo { Title = "Farm Monitoring API", Version = "1.0" });
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT",
                Description = "Paste the JWT access token. Swagger adds the Bearer prefix."
            });
            options.OperationFilter<BearerSecurityOperationFilter>();
            options.AddSecurityDefinition("DeviceKey", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.ApiKey, In = ParameterLocation.Header, Name = "X-Api-Key",
                Description = "Gateway API key. Provide X-Gateway-Code as well."
            });
            options.AddSecurityDefinition("GatewayCode", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.ApiKey, In = ParameterLocation.Header, Name = "X-Gateway-Code"
            });
        });
        return services;
    }
}
