using FarmMonitoring.Application.Features.TechnicalSupport;

namespace FarmMonitoring.UnitTests;

public sealed class DeviceIncidentTests
{
    [Theory]
    [InlineData(0, "Problem", null)]
    [InlineData(1, " ", null)]
    [InlineData(1, "Problem", 0)]
    public void Invalid_incident_inputs_are_rejected(int assignment, string description, int? mission)
        => Assert.False(new CreateDeviceIncidentValidator().Validate(new CreateDeviceIncidentRequest(assignment, description, mission)).IsValid);

    [Fact]
    public void Description_limit_is_enforced()
    {
        var validator = new CreateDeviceIncidentValidator();
        Assert.True(validator.Validate(new CreateDeviceIncidentRequest(1, new string('x', 4000))).IsValid);
        Assert.False(validator.Validate(new CreateDeviceIncidentRequest(1, new string('x', 4001))).IsValid);
    }
}

