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
        RuleFor(x => x.CustomerName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(30).Matches(@"^\+?[0-9 ()-]{7,30}$")
            .Must(phone => phone is not null && phone.Count(char.IsAsciiDigit) is >= 7 and <= 15)
            .WithMessage("Phone must contain 7 to 15 digits.");
        RuleFor(x => x.Email).MaximumLength(255).EmailAddress().When(x => !string.IsNullOrEmpty(x.Email));
        RuleFor(x => x.FarmLocation).NotEmpty().MaximumLength(500);
        RuleFor(x => x.FarmArea).GreaterThan(0).LessThanOrEqualTo(100000000);
        RuleFor(x => x.ServiceNeed).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Content).NotEmpty().MaximumLength(4000);
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
