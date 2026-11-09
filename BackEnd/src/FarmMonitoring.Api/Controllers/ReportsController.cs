using FarmMonitoring.Api.Authorization;
using FarmMonitoring.Api.Contracts;
using FarmMonitoring.Application.Features.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmMonitoring.Api.Controllers;

[ApiController]
[Authorize(Policy = AccessPolicies.ReadFarmData)]
[Produces("application/json")]
[ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ApiError>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
public sealed class ReportsController(ReportService reports) : ControllerBase
{
    [HttpGet("api/dashboard/overview")]
    public async Task<ActionResult<ApiResponse<DashboardOverview>>> Dashboard([FromQuery] int? farmId, CancellationToken ct) =>
        Ok(ApiResponse<DashboardOverview>.Ok(await reports.DashboardAsync(farmId, ct)));
    [HttpGet("api/reports/sensor-data")]
    public async Task<ActionResult<ApiPageResponse<SensorReportRow>>> Sensors([FromQuery] ReportQuery query, CancellationToken ct) =>
        Ok(ApiPageResponse<SensorReportRow>.From(await reports.SensorsAsync(query, ct)));
    [HttpGet("api/reports/missions")]
    public async Task<ActionResult<ApiPageResponse<MissionReportRow>>> Missions([FromQuery] ReportQuery query, CancellationToken ct) =>
        Ok(ApiPageResponse<MissionReportRow>.From(await reports.MissionsAsync(query, ct)));
    [HttpGet("api/reports/alerts")]
    public async Task<ActionResult<ApiPageResponse<AlertReportRow>>> Alerts([FromQuery] ReportQuery query, CancellationToken ct) =>
        Ok(ApiPageResponse<AlertReportRow>.From(await reports.AlertsAsync(query, ct)));
    [HttpGet("api/reports/devices")]
    public async Task<ActionResult<ApiPageResponse<DeviceReportRow>>> Devices([FromQuery] DeviceReportQuery query, CancellationToken ct) =>
        Ok(ApiPageResponse<DeviceReportRow>.From(await reports.DevicesAsync(query, ct)));
}
