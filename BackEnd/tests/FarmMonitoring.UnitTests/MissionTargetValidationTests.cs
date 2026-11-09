using FarmMonitoring.Application.Features.Missions;

namespace FarmMonitoring.UnitTests;

public class MissionTargetValidationTests
{
    private static MissionRequest Plan(MissionTargetRequest[]? assignments, int[]? ids = null) =>
        new("Mission", 1, 1, 1, null, null, ids,
            [new(2, null, null, 0.5m, 0m, 1m, "COLLECT", 10)], null, assignments);

    [Fact]
    public void Explicit_assignments_allow_omitted_legacy_ids_but_reject_conflicting_sets()
    {
        var validator = new MissionValidator();
        Assert.True(validator.Validate(Plan([new(5, 2, 1)])).IsValid);
        Assert.True(validator.Validate(Plan([new(5, 2, 1)], [5])).IsValid);
        Assert.False(validator.Validate(Plan([new(5, 2, 1)], [6])).IsValid);
        Assert.False(validator.Validate(Plan([new(5, 2, 1)], [])).IsValid);
        Assert.True(validator.Validate(Plan(null, [5])).IsValid); // draft only
    }
    [Theory]
    [InlineData(0, 2, 1)]
    [InlineData(5, 0, 1)]
    [InlineData(5, 99, 1)]
    [InlineData(5, 2, 0)]
    public void Invalid_identity_or_mapping_is_rejected(int sensor, int waypoint, int sequence) =>
        Assert.False(new MissionValidator().Validate(Plan([new(sensor, waypoint, sequence)])).IsValid);
    [Fact]
    public void Duplicate_and_null_assignments_are_rejected_without_exceptions()
    {
        var validator = new MissionValidator();
        Assert.False(validator.Validate(Plan([new(5, 2, 1), new(5, 2, 2)])).IsValid);
        Assert.False(validator.Validate(Plan([new(5, 2, 1), new(6, 2, 1)])).IsValid);
        Assert.False(validator.Validate(Plan([null!])).IsValid);
    }
    [Theory]
    [InlineData(-0.001, false)]
    [InlineData(0.35, true)]
    [InlineData(0.6, true)]
    [InlineData(1.0, true)]
    [InlineData(10000000, false)]
    public void Altitude_validation_uses_storage_bounds_not_esp32_ceiling(double altitude, bool valid) =>
        Assert.Equal(valid, new WaypointValidator().Validate(new WaypointRequest(1, null, null, 0, 0, (decimal)altitude, null, null)).IsValid);
}
