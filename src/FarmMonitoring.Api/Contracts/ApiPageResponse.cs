using FarmMonitoring.Application.Common;

namespace FarmMonitoring.Api.Contracts;

public sealed record Pagination(int Page, int PageSize, int TotalItems, int TotalPages);
public sealed record ApiPageResponse<T>(bool Success, IReadOnlyList<T> Data, Pagination Pagination)
{
    public static ApiPageResponse<T> From(PagedResult<T> page) => new(true, page.Items,
        new(page.Page, page.PageSize, page.TotalItems, (int)Math.Ceiling((double)page.TotalItems / page.PageSize)));
}
