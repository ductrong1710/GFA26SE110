using FarmMonitoring.Application.Features.Farms;

namespace FarmMonitoring.UnitTests;

public class FarmValidationTests
{
    [Theory]
    [InlineData(-90, -180, true)]
    [InlineData(90, 180, true)]
    [InlineData(91, 0, false)]
    [InlineData(0, -181, false)]
    public void Coordinates_use_geographic_bounds(int latitude, int longitude, bool valid)
    {
        Assert.Equal(valid, new FarmValidator().Validate(new FarmRequest("Farm", null, latitude, longitude)).IsValid);
        Assert.Equal(valid, new ZoneValidator().Validate(new ZoneRequest("Zone", null, latitude, longitude)).IsValid);
    }

    [Fact]
    public void Locations_are_optional_but_names_and_status_values_are_required()
    {
        Assert.True(new FarmValidator().Validate(new FarmRequest("Demo", null, null, null)).IsValid);
        Assert.True(new ZoneValidator().Validate(new ZoneRequest("Demo", null, null, null)).IsValid);
        Assert.False(new FarmValidator().Validate(new FarmRequest(" ", null, null, null)).IsValid);
        Assert.False(new ZoneValidator().Validate(new ZoneRequest(new string('x', 151), null, null, null)).IsValid);
        Assert.False(new FarmStatusValidator().Validate(new FarmStatusRequest(null)).IsValid);
        Assert.True(new FarmStatusValidator().Validate(new FarmStatusRequest(false)).IsValid);
    }
}
