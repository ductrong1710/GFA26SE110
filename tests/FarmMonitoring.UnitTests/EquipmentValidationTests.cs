using FarmMonitoring.Application.Features.Equipment;

namespace FarmMonitoring.UnitTests;

public class EquipmentValidationTests
{
    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void Equipment_battery_is_bounded(int battery, bool valid)
    {
        Assert.Equal(valid, new UavValidator().Validate(new UavRequest("U1", "UAV", null, "ONLINE", battery, null)).IsValid);
        Assert.Equal(valid, new GatewayValidator().Validate(new GatewayRequest("G1", "Gateway", "ESP32", "ONLINE", null, battery, null, null)).IsValid);
    }
    [Fact]
    public void Equipment_identity_status_and_assignment_are_validated()
    {
        Assert.False(new UavValidator().Validate(new UavRequest("", "", null, "", null, null)).IsValid);
        Assert.False(new GatewayValidator().Validate(new GatewayRequest("G1", "G", "", "ONLINE", -1, null, null, null)).IsValid);
        Assert.False(new EquipmentStatusValidator().Validate(new EquipmentStatusRequest("OFFLINE", null)).IsValid);
        Assert.False(new AssignUavValidator().Validate(new AssignUavRequest(0)).IsValid);
        Assert.True(new AssignUavValidator().Validate(new AssignUavRequest(null)).IsValid);
    }
}
