using Microsoft.EntityFrameworkCore;

using Aonik.Application.Abstractions.Persistence;
using Aonik.Application.Models.Catalog;

namespace Aonik.Application.Services.Catalog;

public class PublicCatalogService : IPublicCatalogService
{
    private readonly IAonikDbContext _dbContext;

    public PublicCatalogService(IAonikDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<CatalogCountryResponse> GetCountriesAsync(
        CatalogCountryListRequest request,
        CancellationToken cancellationToken = default)
    {
        var countries = await _dbContext.Countries
            .AsNoTracking()
            .Where(country => country.IsActive)
            .OrderBy(country => country.Name)
            .Select(country => new CatalogCountryItem(country.IsoAlpha2, country.Name))
            .ToListAsync(cancellationToken);

        var capabilityType = NormalizeCapabilityType(request.CapabilityType);
        var shouldFilterByServices = request.OnlyServiceCountries || !string.IsNullOrWhiteSpace(capabilityType);

        if (shouldFilterByServices && countries.Count > 0)
        {
            var serviceCountriesQuery = _dbContext.CatalogBillers
                .AsNoTracking()
                .Where(biller => biller.IsActive)
                .Join(_dbContext.CatalogBillerServices.AsNoTracking().Where(service => service.IsActive),
                    biller => biller.Id,
                    service => service.BillerId,
                    (biller, service) => new { biller.CountryCode, service.ServiceCode });

            if (!string.IsNullOrWhiteSpace(capabilityType))
            {
                var prefix = capabilityType + "%";
                serviceCountriesQuery = serviceCountriesQuery
                    .Where(item => item.ServiceCode != "" && EF.Functions.Like(item.ServiceCode, prefix));
            }

            var activeCountries = await serviceCountriesQuery
                .Select(item => item.CountryCode)
                .Distinct()
                .ToListAsync(cancellationToken);

            var allowed = new HashSet<string>(activeCountries, StringComparer.OrdinalIgnoreCase);
            countries = countries.Where(item => allowed.Contains(item.CountryCode)).ToList();
        }

        return new CatalogCountryResponse(countries);
    }

    private static string? NormalizeCapabilityType(string? capabilityType)
    {
        return string.IsNullOrWhiteSpace(capabilityType)
            ? null
            : capabilityType.Trim().ToUpperInvariant();
    }
}
