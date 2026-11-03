using FluentValidation;

namespace FarmMonitoring.Application.Features.Consultations;

public sealed record ProposalLineRequest(int SurveyZoneId, string DeviceType, int Quantity, string InstallationLocation = "", int? PublicContentId = null);
public sealed record ProposalRequest(int ExpectedRevision, int SurveyId, int RentalMonths, DateTimeOffset RequestedInstallationAt,
    string? Notes, ProposalLineRequest[] Lines);
public sealed record ProposalConfirmationRequest(string Note);
public sealed record ProposalSummary(int Id, int Revision, int SurveyId, string Status, DateTimeOffset CreatedAt);
public sealed record ProposalLineResponse(int Id, int SurveyZoneId, string DeviceType, int Quantity, string InstallationLocation = "", int? PublicContentId = null,
    int? PriceRevision = null, decimal? MonthlyRentVnd = null, string? ProductName = null);
public sealed record ProposalResponse(int Id, int ConsultationId, int SurveyId, int Revision, int RentalMonths,
    DateTimeOffset RequestedInstallationAt, string? Notes, string Status, int CreatedByUserId, DateTimeOffset CreatedAt,
    int? PlanConfirmedByUserId, DateTimeOffset? PlanConfirmedAt, string? PlanConfirmationNote,
    int? DepositConfirmedByUserId, DateTimeOffset? DepositConfirmedAt, string? DepositConfirmationNote, IReadOnlyList<ProposalLineResponse> Lines);
public sealed record InstallationRequestResponse(int Id, int ProposalId, int FarmId, int OwnerId, string Status, DateTimeOffset SubmittedAt);

public sealed class ProposalValidator : AbstractValidator<ProposalRequest>
{
    public ProposalValidator(TimeProvider clock)
    {
        RuleFor(x => x.ExpectedRevision).InclusiveBetween(0, int.MaxValue - 1);
        RuleFor(x => x.SurveyId).GreaterThan(0);
        RuleFor(x => x.RentalMonths).InclusiveBetween(1, 120);
        RuleFor(x => x.RequestedInstallationAt).Must(x => x > clock.GetUtcNow()).WithMessage("Requested installation must be in the future.");
        RuleFor(x => x.Notes).MaximumLength(4000);
        RuleFor(x => x.Lines).NotNull().Must(x => x is { Length: > 0 and <= 100 }).WithMessage("Provide 1 to 100 proposal lines.");
        RuleFor(x => x.Lines).Must(x => x is null || x.Sum(l => (long)(l?.Quantity ?? 0)) <= 1000).WithMessage("A proposal can contain at most 1000 devices in total.");
        RuleForEach(x => x.Lines).NotNull().SetValidator(new ProposalLineValidator());
    }
}
public sealed class ProposalLineValidator : AbstractValidator<ProposalLineRequest>
{
    public ProposalLineValidator()
    {
        RuleFor(x => x.SurveyZoneId).GreaterThan(0);
        RuleFor(x => x.DeviceType).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Quantity).InclusiveBetween(1, 1000);
        RuleFor(x => x.InstallationLocation).MaximumLength(1000);
    }
}
public sealed class ProposalConfirmationValidator : AbstractValidator<ProposalConfirmationRequest>
{
    public ProposalConfirmationValidator() => RuleFor(x => x.Note).NotEmpty().MaximumLength(2000);
}
