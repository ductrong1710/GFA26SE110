using FarmMonitoring.Application.Common;
using FluentValidation;

namespace FarmMonitoring.Application.Features.Telemetry;

public sealed record TelemetryRequest(int MissionId, int? UavId, int? GatewayId, DateTimeOffset RecordedAt,
    decimal? Latitude, decimal? Longitude, decimal? LocalX, decimal? LocalY, decimal? AltitudeM,
    decimal? BatteryPercent, int? CurrentWaypointNo, string? FlightStatus);
public sealed record TelemetryResponse(long Id, int MissionId, int? UavId, int? GatewayId, DateTimeOffset RecordedAt,
    decimal? Latitude, decimal? Longitude, decimal? LocalX, decimal? LocalY, decimal? AltitudeM,
    decimal? BatteryPercent, int? CurrentWaypointNo, string? FlightStatus);
public sealed class TelemetryQuery : PageQuery
{
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
}
public sealed class TelemetryValidator : AbstractValidator<TelemetryRequest>
{
    public TelemetryValidator(TimeProvider clock)
    {
        RuleFor(x => x.MissionId).GreaterThan(0);
        RuleFor(x => x.UavId).GreaterThan(0);
        RuleFor(x => x.GatewayId).GreaterThan(0);
        RuleFor(x => x.RecordedAt).NotEmpty().Must(x => x <= clock.GetUtcNow().AddMinutes(5)).WithMessage("Recorded time cannot be in the future beyond five minutes of clock skew.");
        RuleFor(x => x.Latitude).InclusiveBetween(-90m, 90m);
        RuleFor(x => x.Longitude).InclusiveBetween(-180m, 180m);
        RuleFor(x => x.LocalX).InclusiveBetween(-9999999.999m, 9999999.999m);
        RuleFor(x => x.LocalY).InclusiveBetween(-9999999.999m, 9999999.999m);
        RuleFor(x => x.AltitudeM).InclusiveBetween(-9999999.999m, 9999999.999m);
        RuleFor(x => x.BatteryPercent).InclusiveBetween(0m, 100m);
        RuleFor(x => x.CurrentWaypointNo).GreaterThan(0);
        RuleFor(x => x.FlightStatus).MaximumLength(50);
        RuleFor(x => x).Must(x => x.Latitude.HasValue == x.Longitude.HasValue && x.LocalX.HasValue == x.LocalY.HasValue)
            .WithMessage("Supply both values for each coordinate pair.");
    }
}
public sealed class TelemetryQueryValidator : AbstractValidator<TelemetryQuery>
{
    public TelemetryQueryValidator()
    {
        Include(new PageQueryValidator());
        RuleFor(x => x).Must(x => !x.From.HasValue || !x.To.HasValue || x.To >= x.From).WithMessage("Invalid date range.");
    }
}
