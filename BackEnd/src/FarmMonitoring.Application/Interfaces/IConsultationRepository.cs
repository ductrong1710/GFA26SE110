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
    Task ClaimAsync(int id, int engineerId, DateTimeOffset now, CancellationToken ct);
    Task FollowUpAsync(int id, int engineerId, FollowUpRequest request, DateTimeOffset now, CancellationToken ct);
    Task AddSurveyAsync(FarmSurvey survey, int expectedRevision, CancellationToken ct);
    Task<PagedResult<SurveySummary>> ListSurveysAsync(int consultationId, PageQuery query, CancellationToken ct);
    Task<FarmSurvey?> GetSurveyAsync(int consultationId, int surveyId, CancellationToken ct);
}
