using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Interfaces;
using FluentValidation;

namespace FarmMonitoring.Application.Features.Consultations;

public sealed record DossierCustomerRequest(string FullName, string Email);
public sealed record DossierSubmissionRequest(int ExpectedRevision, string Note, EvidenceImageData[] Bills, DossierCustomerRequest? Customer = null);
public sealed record DossierReviewRequest(int ExpectedRevision, bool Approve, string Note);
public sealed record DossierSummary(int Id, int ConsultationId, int Revision, string CustomerName, string Phone,
    string FarmName, string FarmLocation, DateTimeOffset? SubmittedAt);
public sealed record DossierDecision(int ProposalId, string Status, int? InstallationRequestId);

public sealed class InstallationDossierService(IInstallationDossierRepository repository, ICurrentUser current,
    IValidator<PageQuery> pages, TimeProvider clock)
{
    private int Actor => current.UserId ?? throw new AuthException("Authentication required.");
    public async Task<PagedResult<DossierSummary>> ListAsync(PageQuery query, CancellationToken ct)
    {
        await pages.ValidateAndThrowAsync(query, ct);
        return await repository.ListAsync(Actor, query, ct);
    }
    public Task<DossierDecision> SubmitAsync(int id, DossierSubmissionRequest request, CancellationToken ct)
    {
        Validate(request.ExpectedRevision, request.Note);
        if (request.Customer is { } customer && (string.IsNullOrWhiteSpace(customer.FullName) || customer.FullName.Length > 150 ||
            string.IsNullOrWhiteSpace(customer.Email) || customer.Email.Length > 255 ||
            !System.Net.Mail.MailAddress.TryCreate(customer.Email.Trim(), out var email) || email.Address != customer.Email.Trim()))
            throw new ValidationException("Provide a customer name and a valid email for the rental account.");
        var bills = EvidenceImages.Encode(EvidenceImages.Decode(request.Bills));
        if (bills.Length == 0) throw new ValidationException("Attach the external deposit bill before submitting the dossier.");
        return repository.SubmitAsync(id, Actor, request.ExpectedRevision, request.Note.Trim(), bills, request.Customer, clock.GetUtcNow(), ct);
    }
    public Task<DossierDecision> ReviewAsync(int id, DossierReviewRequest request, CancellationToken ct)
    {
        Validate(request.ExpectedRevision, request.Note);
        return repository.ReviewAsync(id, Actor, request, clock.GetUtcNow(), ct);
    }
    private static void Validate(int revision, string? note)
    {
        if (revision <= 0 || string.IsNullOrWhiteSpace(note) || note.Length > 2000)
            throw new ValidationException("A positive proposal revision and a note of 1 to 2000 characters are required.");
    }
}
