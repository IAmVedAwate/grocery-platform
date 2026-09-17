namespace Application.Common;

/// <summary>
/// List endpoint envelope (docs/api/api-conventions.md). TotalCount MUST be
/// produced using the exact same filter predicate as Items — the
/// repository methods that build this are written so that discipline is
/// structural, not something each caller has to remember.
/// </summary>
public sealed class PagedResult<T>
{
    public required IReadOnlyList<T> Items { get; init; }
    public required int Page { get; init; }
    public required int PageSize { get; init; }
    public required int TotalCount { get; init; }
}

public sealed class PageRequest
{
    private const int MaxPageSize = 100;
    public int Page { get; }
    public int PageSize { get; }

    public PageRequest(int page, int pageSize)
    {
        Page = page < 1 ? 1 : page;
        PageSize = pageSize switch
        {
            < 1 => 20,
            > MaxPageSize => MaxPageSize,
            _ => pageSize
        };
    }

    public int Skip => (Page - 1) * PageSize;
}
