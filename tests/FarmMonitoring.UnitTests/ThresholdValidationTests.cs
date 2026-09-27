using FarmMonitoring.Application.Features.Thresholds;

namespace FarmMonitoring.UnitTests;

public class ThresholdValidationTests
{
    [Fact]
    public void Threshold_supports_optional_bounds_and_rejects_inverted_bounds()
    {
        var validator = new ThresholdValidator();
        Assert.True(validator.Validate(new ThresholdRequest(null, 30, null, null)).IsValid);
        Assert.True(validator.Validate(new ThresholdRequest(10, null, 60, 20)).IsValid);
        Assert.True(validator.Validate(new ThresholdRequest(10, 10, null, null)).IsValid);
        Assert.False(validator.Validate(new ThresholdRequest(30, 10, null, null)).IsValid);
    }
    [Fact]
    public void Threshold_limits_match_storage_and_positive_timeout()
    {
        var validator = new ThresholdValidator();
        Assert.False(validator.Validate(new ThresholdRequest(null, 1000000000000m, null, null)).IsValid);
        Assert.False(validator.Validate(new ThresholdRequest(null, null, 0, null)).IsValid);
        Assert.False(validator.Validate(new ThresholdRequest(null, null, 10, 101)).IsValid);
        Assert.False(validator.Validate(new ThresholdRequest(null, null, 10, -1)).IsValid);
    }
}
