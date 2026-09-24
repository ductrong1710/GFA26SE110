using FluentValidation;

namespace FarmMonitoring.Application.Common;

public class PageQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Search { get; init; }
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalItems, int Page, int PageSize);

public class PageQueryValidator : AbstractValidator<PageQuery>
{
    public PageQueryValidator()
    {
        RuleFor(x => x.Page).InclusiveBetween(1, int.MaxValue / 100).OverridePropertyName("page");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).OverridePropertyName("pageSize");
        RuleFor(x => x.Search).MaximumLength(255).OverridePropertyName("search");
    }
}
