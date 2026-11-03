using FarmMonitoring.Api.Contracts;
using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Application.Features.TechnicalSupport;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmMonitoring.Api.Controllers;
[ApiController, Authorize, Route("api/operations")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class OperationsController(OperationsService repository, ICurrentUser current, IValidator<PageQuery> pages) : ControllerBase
{
    private int Actor => current.UserId ?? throw new AuthException("Authentication required.");
    private static void Period(DateTimeOffset from, DateTimeOffset to)
    {
        if (from == default || to <= from || to - from > TimeSpan.FromDays(366))
            throw new ValidationException("Choose a period up to 366 days with end after start.");
    }
    [HttpGet("calendar")]
    public async Task<object> Calendar(DateTimeOffset from, DateTimeOffset to, int? technicianId, CancellationToken ct)
    { Period(from,to); return ApiResponse<object>.Ok(await repository.CalendarAsync(Actor,from,to,technicianId,ct)); }
    [HttpGet("available-technicians")]
    public async Task<object> Available(DateTimeOffset from, DateTimeOffset to, string? excludeType, int? excludeId, CancellationToken ct)
    { Period(from,to); return ApiResponse<object>.Ok(await repository.AvailableAsync(Actor,from,to,excludeType,excludeId,ct)); }
    [HttpGet("dashboard")]
    public async Task<object> Dashboard(DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    { Period(from,to); return ApiResponse<object>.Ok(await repository.DashboardAsync(Actor,from,to,ct)); }
    [HttpGet("consultation-requests")]
    public async Task<object> Leads(DateTimeOffset from, DateTimeOffset to, [FromQuery] PageQuery query, CancellationToken ct)
    { Period(from,to); await pages.ValidateAndThrowAsync(query,ct); return ApiResponse<object>.Ok(await repository.LeadsAsync(Actor,from,to,query,ct)); }
    [HttpGet("device-history")]
    public async Task<object> History(string code, [FromQuery] PageQuery query, CancellationToken ct)
    { if (string.IsNullOrWhiteSpace(code) || code.Length > 200) throw new ValidationException("Enter device code."); await pages.ValidateAndThrowAsync(query,ct); return ApiResponse<object>.Ok(await repository.DeviceHistoryAsync(Actor,code,query,ct)); }
    [HttpGet("{jobType}/{jobId:int}/installed-devices")]
    public async Task<object> Installed(string jobType, int jobId, [FromQuery] PageQuery query, CancellationToken ct)
    { await pages.ValidateAndThrowAsync(query,ct); return ApiResponse<object>.Ok(await repository.InstalledAsync(Actor,jobType,jobId,query,ct)); }
    [HttpGet("service-jobs/{jobId:int}/plans")]
    public async Task<object> Plans(int jobId, CancellationToken ct) => ApiResponse<object>.Ok(await repository.PlansAsync(Actor,jobId,ct));
    [HttpPost("service-jobs/{jobId:int}/plans")]
    public async Task<object> Propose(int jobId, ProposeServicePlan request, CancellationToken ct) => ApiResponse<object>.Ok(await repository.ProposeAsync(Actor,jobId,request,ct));
    [HttpPost("service-plans/{planId:int}/review")]
    public async Task<object> Review(int planId, ReviewServicePlan request, CancellationToken ct) => ApiResponse<object>.Ok(await repository.ReviewAsync(Actor,planId,request,ct));
    [HttpPost("service-plans/{planId:int}/execute"), RequestSizeLimit(45 * 1024 * 1024)]
    public async Task<object> Execute(int planId, ExecuteServicePlan request, CancellationToken ct) => ApiResponse<object>.Ok(await repository.ExecuteAsync(Actor,planId,request,ct));
}
