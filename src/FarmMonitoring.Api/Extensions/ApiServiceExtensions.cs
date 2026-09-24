using System.Text;
using FarmMonitoring.Api.Authorization;
using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Users;
using FarmMonitoring.Application.Features.Farms;
using FarmMonitoring.Application.Features.Sensors;
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
        services.AddScoped<UserService>();
        services.AddScoped<FarmService>();
        services.AddScoped<SensorService>();
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

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
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
                        await context.Response.WriteAsJsonAsync(ApiError.Create(context.HttpContext, "Access denied."));
                    }
                };
            });
        services.AddScoped<IAuthorizationHandler, CurrentRoleHandler>();
        services.AddAuthorization(options =>
        {
            options.AddPolicy(AccessPolicies.ManageUsers, policy => policy.RequireAuthenticatedUser()
                .AddRequirements(new CurrentRoleRequirement(RoleNames.FarmAdministrator)));
            options.AddPolicy(AccessPolicies.ManageFarms, policy => policy.RequireAuthenticatedUser()
                .AddRequirements(new CurrentRoleRequirement(RoleNames.FarmAdministrator)));
            options.AddPolicy(AccessPolicies.ReadFarmData, policy => policy.RequireAuthenticatedUser()
                .AddRequirements(new CurrentRoleRequirement(RoleNames.FarmAdministrator, RoleNames.UavDeviceOperator)));
            options.AddPolicy(AccessPolicies.ManageDevices, policy => policy.RequireAuthenticatedUser()
                .AddRequirements(new CurrentRoleRequirement(RoleNames.UavDeviceOperator)));
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
        });
        return services;
    }
}
