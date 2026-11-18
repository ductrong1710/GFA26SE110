using FarmMonitoring.Api.Contracts;
using FarmMonitoring.Api.Extensions;
using FarmMonitoring.Api.Middleware;
using FarmMonitoring.Infrastructure;
using FarmMonitoring.Infrastructure.Persistence;
using Microsoft.AspNetCore.HttpOverrides;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

var forwardedHeadersEnabled = builder.Configuration.GetValue<bool>("ForwardedHeaders:Enabled");
if (forwardedHeadersEnabled)
{
    var trustedProxies = builder.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [];
    if (trustedProxies.Length == 0)
        throw new InvalidOperationException("ForwardedHeaders:KnownProxies must contain at least one trusted proxy address when forwarded headers are enabled.");

    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
        options.ForwardLimit = 1;
        foreach (var proxy in trustedProxies)
            options.KnownProxies.Add(IPAddress.Parse(proxy));
    });
}

builder.Services.AddInfrastructure(builder.Configuration, builder.Environment.IsDevelopment());
builder.Services.AddApiServices();
builder.Services.AddCors(options => options.AddPolicy("Frontend", policy =>
{
    var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    policy.WithOrigins(origins.Where(origin => !string.IsNullOrWhiteSpace(origin)).ToArray())
        .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
        .WithHeaders("Authorization", "Content-Type");
}));

var app = builder.Build();
if (forwardedHeadersEnabled) app.UseForwardedHeaders();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseStatusCodePages(async context =>
{
    var http = context.HttpContext;
    await http.Response.WriteAsJsonAsync(ApiError.Create(http, http.Response.StatusCode switch
    {
        404 => "Resource not found.",
        405 => "Method not allowed.",
        415 => "Unsupported media type.",
        _ => "Request failed."
    }));
});
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<DevelopmentAdminSeeder>().SeedAsync(
        app.Configuration["SEED_ADMIN_EMAIL"], app.Configuration["SEED_ADMIN_PASSWORD"], app.Lifetime.ApplicationStopping);
}
if (builder.Configuration.GetValue<bool?>("HttpsRedirection:Enabled") ?? !app.Environment.IsDevelopment())
    app.UseHttpsRedirection();
app.UseRouting();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

public partial class Program;
