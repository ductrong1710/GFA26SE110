using FarmMonitoring.Application.Features.CropSupport;

namespace FarmMonitoring.UnitTests;
public sealed class CropSupportValidationTests
{
    private static readonly CreateCropConditionRequest Valid = new(1, 2, "Tomato", "Leaves turning yellow", [3]);
    [Fact]
    public void Intake_requires_scope_crop_symptoms_and_bounded_distinct_sensor_references()
    {
        var validator = new CreateCropConditionValidator();
        Assert.True(validator.Validate(Valid).IsValid);
        Assert.True(validator.Validate(Valid with { ZoneId = null, SensorChannelIds = null }).IsValid);
        Assert.False(validator.Validate(Valid with { FarmId = 0 }).IsValid);
        Assert.False(validator.Validate(Valid with { ZoneId = 0 }).IsValid);
        Assert.False(validator.Validate(Valid with { Crop = " " }).IsValid);
        Assert.False(validator.Validate(Valid with { Description = " " }).IsValid);
        Assert.False(validator.Validate(Valid with { Crop = new string('x', 201) }).IsValid);
        Assert.False(validator.Validate(Valid with { Description = new string('x', 4001) }).IsValid);
        Assert.False(validator.Validate(Valid with { SensorChannelIds = [3, 3] }).IsValid);
        Assert.False(validator.Validate(Valid with { SensorChannelIds = [0] }).IsValid);
        Assert.False(validator.Validate(Valid with { SensorChannelIds = Enumerable.Range(1, 51).ToArray() }).IsValid);
    }
    [Fact]
    public void Communication_requires_content_and_current_revision()
    {
        var validator = new CropConsultationValidator();
        var valid = new CropConsultationRequest(1, "Inspect leaf undersides", true);
        Assert.True(validator.Validate(valid).IsValid);
        Assert.False(validator.Validate(valid with { ExpectedRevision = 0 }).IsValid);
        Assert.False(validator.Validate(valid with { ExpectedRevision = int.MaxValue }).IsValid);
        Assert.False(validator.Validate(valid with { Content = " " }).IsValid);
        Assert.False(validator.Validate(valid with { Content = new string('x', 4001) }).IsValid);
    }
}
