using FarmMonitoring.Api.Contracts;
using FarmMonitoring.Api.Extensions;
using FarmMonitoring.Api.Middleware;
using FarmMonitoring.Infrastructure;
using FarmMonitoring.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApiServices();

var app = builder.Build();
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
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

public partial class Program;
