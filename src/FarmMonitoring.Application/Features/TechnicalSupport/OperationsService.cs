using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Interfaces;

namespace FarmMonitoring.Application.Features.TechnicalSupport;
public sealed class OperationsService(IOperationsRepository repository)
{
    public Task<object> CalendarAsync(int actor, DateTimeOffset? from, DateTimeOffset? to,int? tech,CancellationToken ct) => repository.CalendarAsync(actor,from,to,tech,ct);
    public Task<object> AvailableAsync(int actor,DateTimeOffset from,DateTimeOffset to,string? type,int? id,CancellationToken ct) => repository.AvailableAsync(actor,from,to,type,id,ct);
    public Task<object> DashboardAsync(int actor,DateTimeOffset from,DateTimeOffset to,CancellationToken ct) => repository.DashboardAsync(actor,from,to,ct);
    public Task<object> LeadsAsync(int actor,DateTimeOffset? from,DateTimeOffset? to,PageQuery query,CancellationToken ct) => repository.LeadsAsync(actor,from,to,query,ct);
    public Task<object> DeviceHistoryAsync(int actor,string code,PageQuery query,CancellationToken ct) => repository.DeviceHistoryAsync(actor,code,query,ct);
    public Task<object> InstalledAsync(int actor,string type,int id,PageQuery query,CancellationToken ct) => repository.InstalledAsync(actor,type,id,query,ct);
    public Task<object> PlansAsync(int actor,int job,CancellationToken ct) => repository.PlansAsync(actor,job,ct);
    public Task<object> ProposeAsync(int actor,int job,ProposeServicePlan request,CancellationToken ct) => repository.ProposeAsync(actor,job,request,ct);
    public Task<object> ReviewAsync(int actor,int id,ReviewServicePlan request,CancellationToken ct) => repository.ReviewAsync(actor,id,request,ct);
    public Task<object> ExecuteAsync(int actor,int id,ExecuteServicePlan request,CancellationToken ct) => repository.ExecuteAsync(actor,id,request,ct);
}
