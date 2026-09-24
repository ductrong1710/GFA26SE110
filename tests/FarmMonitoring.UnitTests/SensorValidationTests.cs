using FarmMonitoring.Application.Features.Sensors;

namespace FarmMonitoring.UnitTests;

public class SensorValidationTests
{
    [Fact]
    public void Sensor_identity_location_and_battery_are_validated()
    {
        var valid = new SensorNodeRequest(1, "SN-1", "Node", "OFFLINE", null, null, 1, 2, null, 50);
        var validator = new SensorNodeValidator();
        Assert.True(validator.Validate(valid).IsValid);
        Assert.False(validator.Validate(valid with { ZoneId = 0 }).IsValid);
        Assert.False(validator.Validate(valid with { DeviceCode = " " }).IsValid);
        Assert.False(validator.Validate(valid with { Latitude = 91 }).IsValid);
        Assert.False(validator.Validate(valid with { BatteryPercent = -1 }).IsValid);
        Assert.False(validator.Validate(valid with { LocalX = 10000000 }).IsValid);
    }

    [Fact]
    public void Channel_type_and_code_are_required()
    {
        var validator = new SensorChannelValidator();
        Assert.True(validator.Validate(new SensorChannelRequest(1, "temperature", null, true)).IsValid);
        Assert.False(validator.Validate(new SensorChannelRequest(0, "", null, true)).IsValid);
        Assert.False(validator.Validate(new SensorChannelRequest(1, new string('x', 101), null, true)).IsValid);
    }
}
