using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Consultations;
using FarmMonitoring.Domain.Entities;

namespace FarmMonitoring.Application.Interfaces;

public interface IConsultationRepository
{
    Task CreateAsync(Consultation consultation, CancellationToken ct);
    Task<PagedResult<ConsultationSummary>> ListAsync(int engineerId, bool administrator, ConsultationQuery query, CancellationToken ct);
    Task BindCustomerAsync(int id, int administratorId, CustomerFarmRequest request, DateTimeOffset now, CancellationToken ct);
    Task<PagedResult<ConsultationSummary>> FarmConsultationsAsync(int farmId, int userId, bool administrator, PageQuery query, CancellationToken ct);
    Task<Consultation?> GetAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<ConsultationAssignmentCandidate>> ListAssignmentCandidatesAsync(int id, CancellationToken ct);
    Task RequestReturnAsync(int id, int engineerId, string reason, DateTimeOffset now, CancellationToken ct);
    Task ReviewReturnAsync(int id, int adminId, ConsultationReturnDecision decision, DateTimeOffset now, CancellationToken ct);
    Task CloseAsync(int id, int engineerId, ConsultationStatus status, string? reason, DateTimeOffset now, CancellationToken ct);
    Task AssignAsync(int id, int administratorId, AssignConsultationRequest request, DateTimeOffset now, CancellationToken ct);
    Task FollowUpAsync(int id, int engineerId, FollowUpRequest request, DateTimeOffset now, CancellationToken ct);
    Task AddSurveyAsync(FarmSurvey survey, int expectedRevision, CancellationToken ct);
    Task<PagedResult<SurveySummary>> ListSurveysAsync(int consultationId, PageQuery query, CancellationToken ct);
    Task<FarmSurvey?> GetSurveyAsync(int consultationId, int surveyId, CancellationToken ct);
}
