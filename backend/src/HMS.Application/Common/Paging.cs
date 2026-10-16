using Microsoft.EntityFrameworkCore;

namespace HMS.Application.Common;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public sealed record PageQuery(int Page = 1, int PageSize = 20)
{
    public const int MaxPageSize = 100;
    public int SafePage => Page < 1 ? 1 : Page;
    public int SafePageSize => PageSize switch { < 1 => 20, > MaxPageSize => MaxPageSize, _ => PageSize };
}

public static class PagingExtensions
{
    public static async Task<PagedResult<T>> ToPagedAsync<T>(this IQueryable<T> query, PageQuery page, CancellationToken ct)
    {
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page.SafePage - 1) * page.SafePageSize).Take(page.SafePageSize).ToListAsync(ct);
        return new PagedResult<T>(items, page.SafePage, page.SafePageSize, total);
    }
}
