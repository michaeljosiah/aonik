namespace Aonik.Api.Endpoints.Catalog;

public static class CatalogValidation
{
    public static string? NormalizeCountryCode(string? countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode))
        {
            return null;
        }

        var normalized = countryCode.Trim().ToUpperInvariant();
        return normalized.Length == 2 ? normalized : null;
    }

    public static string? NormalizeSearch(string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return null;
        }

        var trimmed = search.Trim();
        return trimmed.Length <= 100 ? trimmed : null;
    }

    public static (int Page, int PageSize) NormalizePaging(int page, int pageSize)
    {
        var resolvedPage = page < 1 ? 1 : page;
        var resolvedPageSize = pageSize is < 1 or > 100 ? 20 : pageSize;
        return (resolvedPage, resolvedPageSize);
    }

}
