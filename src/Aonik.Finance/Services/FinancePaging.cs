namespace Aonik.Finance.Services;

/// <summary>
/// Server-side paging bounds for Finance list endpoints (issue H10). These lists
/// historically returned every row for a tenant, so the cap here is a safety bound
/// that applies even when a caller passes nothing — a single call can never pull an
/// unbounded result set. Callers page further with an explicit page number.
/// </summary>
internal static class FinancePaging
{
    /// <summary>Default page size used when a caller does not specify one.</summary>
    public const int DefaultPageSize = 200;

    /// <summary>Hard upper bound on a single page — a caller cannot exceed this.</summary>
    public const int MaxPageSize = 500;

    /// <summary>
    /// Clamps caller-supplied paging into the safe window: page number floored at 1,
    /// page size mapped to <see cref="DefaultPageSize"/> when unset/invalid and capped
    /// at <see cref="MaxPageSize"/>.
    /// </summary>
    public static (int PageNumber, int PageSize) Normalize(int pageNumber, int pageSize)
    {
        var number = pageNumber < 1 ? 1 : pageNumber;
        var size = pageSize switch
        {
            < 1 => DefaultPageSize,
            > MaxPageSize => MaxPageSize,
            _ => pageSize,
        };
        return (number, size);
    }

    /// <summary>Zero-based row offset for the given (already-normalized) page.</summary>
    public static int Offset(int pageNumber, int pageSize) => (pageNumber - 1) * pageSize;
}
