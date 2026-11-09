using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Consultations;
using FarmMonitoring.Application.Features.Installations;
using FarmMonitoring.Application.Features.TechnicalSupport;
using FarmMonitoring.Domain.Entities;
using FluentValidation;

namespace FarmMonitoring.UnitTests;
public sealed class OperationalWorkflowTests
{
    [Theory]
    [InlineData("2026-11-02T01:00:00Z","2026-11-02T17:00:00Z")]
    [InlineData("2026-11-02T16:59:59Z","2026-11-02T17:00:00Z")]
    [InlineData("2026-11-02T17:00:00Z","2026-11-03T17:00:00Z")]
    [InlineData("2026-12-31T18:00:00Z","2027-01-01T17:00:00Z")]
    public void Rental_start_is_next_Vietnam_day_not_next_UTC_day(string completed,string expected) =>
        Assert.Equal(DateTimeOffset.Parse(expected),OperationalRules.RentalStart(DateTimeOffset.Parse(completed)));

    [Fact]
    public void Travel_buffer_rejects_nearby_appointments_but_allows_exact_boundary()
    {
        var start=DateTimeOffset.Parse("2026-11-03T01:00Z");
        Assert.True(OperationalRules.Overlaps(start,start.AddHours(2),start.AddHours(2).AddMinutes(29),start.AddHours(4)));
        Assert.False(OperationalRules.Overlaps(start,start.AddHours(2),start.AddHours(2).AddMinutes(30),start.AddHours(4)));
    }
    [Fact]
    public void Appointment_duration_must_be_positive_and_bounded()
    {
        var start=DateTimeOffset.UtcNow.AddDays(1);
        var request=new JobAssignmentRequest(1,start,"Visit",ScheduledEndAt:start);
        Assert.False(new JobAssignmentValidator(TimeProvider.System).Validate(request).IsValid);
        Assert.True(new JobAssignmentValidator(TimeProvider.System).Validate(request with {ScheduledEndAt=start.AddHours(3)}).IsValid);
    }
    [Fact]
    public void Warranty_charges_zero_and_regular_line_uses_snapshot_price()
    {
        var line=new PlanPriceSnapshot(1,2,"Sensor",3,200000,false,"Outside warranty");
        Assert.Equal(600000,line.TotalVnd);
        Assert.Equal(0,(line with {Warranty=true}).TotalVnd);
    }
    [Fact]
    public void Unknown_device_report_needs_farm_but_no_code_or_photo()
    {
        var validator=new CreateDeviceIncidentValidator();
        Assert.True(validator.Validate(new CreateDeviceIncidentRequest(null,"No sensor data",FarmId:1)).IsValid);
        Assert.False(validator.Validate(new CreateDeviceIncidentRequest(null,"No sensor data")).IsValid);
    }
    [Fact]
    public void Survey_needs_no_plant_age_or_installation_position()
    {
        var request=new SurveyRequest(0,"Farm","Address",2,"Monitoring",null,[new("North","Mango")]);
        Assert.True(new SurveyValidator().Validate(request).IsValid);
    }
    [Fact]
    public void Signed_paper_evidence_cannot_be_empty_or_fake()
    {
        Assert.Throws<ValidationException>(()=>PaperEvidence.Required([],"agreement"));
        Assert.Throws<ValidationException>(()=>PaperEvidence.Required([new("image/png","AAAA")],"agreement"));
    }
}
