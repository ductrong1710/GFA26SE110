using FarmMonitoring.Application.Common;
using FluentValidation;

namespace FarmMonitoring.Application.Features.Consultations;

public sealed record SubmitConsultationRequest(string CustomerName, string Phone, string? Email,
    string FarmLocation, decimal? FarmArea, string ServiceNeed, string Content);
public sealed record FollowUpRequest(string Notes, string ContactResult, string NextAction);
public sealed record CustomerFarmRequest(int OwnerId, int FarmId);
public sealed class ConsultationQuery : PageQuery { public bool Inbox { get; init; } }
public sealed record ConsultationReceipt(int Id, string Status);
public sealed record ConsultationSummary(int Id, string ServiceNeed, string Status, DateTimeOffset CreatedAt);
public sealed record ConsultationHistoryResponse(int Id, int? UserId, string Action, string Status,
    string? Notes, string? ContactResult, string? NextAction, DateTimeOffset CreatedAt);
public sealed record ConsultationResponse(int Id, string CustomerName, string Phone, string? Email,
    string FarmLocation, decimal? FarmArea, string ServiceNeed, string Content, string Status,
    int? EngineerId, DateTimeOffset CreatedAt, IReadOnlyList<ConsultationHistoryResponse> History, int? OwnerId, int? FarmId);

public sealed class SubmitConsultationValidator : AbstractValidator<SubmitConsultationRequest>
{
    public SubmitConsultationValidator()
    {
        RuleFor(x => x.CustomerName).NotEmpty().WithMessage("The customer name field is required.").MaximumLength(150);
        RuleFor(x => x.Phone).Cascade(CascadeMode.Stop).NotEmpty().WithMessage("The phone field is required.").MaximumLength(30).Matches(@"^\+?[0-9 ()-]{7,30}$")
            .WithMessage("Invalid phone format.")
            .Must(phone => phone is not null && phone.Count(char.IsAsciiDigit) is >= 7 and <= 15)
            .WithMessage("Invalid phone format.");
        RuleFor(x => x.Email).MaximumLength(255).EmailAddress().WithMessage("Invalid email format.").When(x => !string.IsNullOrEmpty(x.Email));
        RuleFor(x => x.FarmLocation).NotEmpty().WithMessage("The farm location field is required.").MaximumLength(500);
        RuleFor(x => x.FarmArea).GreaterThan(0).LessThanOrEqualTo(100000000);
        RuleFor(x => x.ServiceNeed).NotEmpty().WithMessage("The service need field is required.").MaximumLength(500);
        RuleFor(x => x.Content).NotEmpty().WithMessage("The content field is required.").MaximumLength(4000);
    }
}

public sealed class FollowUpValidator : AbstractValidator<FollowUpRequest>
{
    public FollowUpValidator()
    {
        RuleFor(x => x.Notes).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.ContactResult).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.NextAction).NotEmpty().MaximumLength(1000);
    }
}
