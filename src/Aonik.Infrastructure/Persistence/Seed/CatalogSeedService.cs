using Aonik.Application.Abstractions.Persistence;
using Aonik.Domain.ReferenceData.Entities;
using Aonik.Domain.Catalog.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aonik.Infrastructure.Persistence.Seed;

public class CatalogSeedService
{
    private static readonly Guid UtilitiesCategoryId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TelecomCategoryId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid InternetCategoryId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid EducationCategoryId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid GovernmentCategoryId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid CableCategoryId = Guid.Parse("66666666-6666-6666-6666-666666666666");

    private readonly IAonikDbContext _dbContext;
    private readonly ILogger<CatalogSeedService> _logger;

    public CatalogSeedService(IAonikDbContext dbContext, ILogger<CatalogSeedService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await SeedCountriesAsync(cancellationToken);
        await SeedCategoriesAsync(cancellationToken);
    }

    private async Task SeedCountriesAsync(CancellationToken cancellationToken)
    {
        var countries = new List<ReferenceDataItem>
        {
            new()
            {
                Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                Type = "Country",
                Code = "GH",
                DisplayName = "Ghana",
                SortOrder = 1,
                IsActive = true
            },
            new()
            {
                Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                Type = "Country",
                Code = "KE",
                DisplayName = "Kenya",
                SortOrder = 2,
                IsActive = true
            },
            new()
            {
                Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                Type = "Country",
                Code = "NG",
                DisplayName = "Nigeria",
                SortOrder = 3,
                IsActive = true
            },
            new()
            {
                Id = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
                Type = "Country",
                Code = "UG",
                DisplayName = "Uganda",
                SortOrder = 4,
                IsActive = true
            },
            new()
            {
                Id = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
                Type = "Country",
                Code = "TZ",
                DisplayName = "Tanzania",
                SortOrder = 5,
                IsActive = true
            },
            new()
            {
                Id = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"),
                Type = "Country",
                Code = "ZA",
                DisplayName = "South Africa",
                SortOrder = 6,
                IsActive = true
            }
        };

        var existingKeys = await _dbContext.ReferenceDataItems
            .Where(item => item.Type == "Country")
            .Select(item => item.Code)
            .ToListAsync(cancellationToken);

        var existingSet = new HashSet<string>(existingKeys, StringComparer.OrdinalIgnoreCase);
        var toAdd = countries.Where(country => !existingSet.Contains(country.Code)).ToList();

        if (toAdd.Count == 0)
        {
            return;
        }

        await _dbContext.ReferenceDataItems.AddRangeAsync(toAdd, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Seeded {Count} countries", toAdd.Count);
    }

    private async Task SeedCategoriesAsync(CancellationToken cancellationToken)
    {
        var categories = new List<CatalogBillerCategory>
        {
            new()
            {
                Id = UtilitiesCategoryId,
                TenantId = Guid.Empty,
                CountryCode = "GH",
                Name = "Utilities",
                Description = "Electricity and water",
                IconUrl = "https://cdn.aonik.io/catalog/icons/utilities.png",
                SortOrder = 1,
                IsActive = true
            },
            new()
            {
                Id = TelecomCategoryId,
                TenantId = Guid.Empty,
                CountryCode = "GH",
                Name = "Telecom",
                Description = "Mobile and fixed line",
                IconUrl = "https://cdn.aonik.io/catalog/icons/telecom.png",
                SortOrder = 2,
                IsActive = true
            },
            new()
            {
                Id = InternetCategoryId,
                TenantId = Guid.Empty,
                CountryCode = "NG",
                Name = "Internet",
                Description = "ISPs and broadband",
                IconUrl = "https://cdn.aonik.io/catalog/icons/internet.png",
                SortOrder = 3,
                IsActive = true
            },
            new()
            {
                Id = EducationCategoryId,
                TenantId = Guid.Empty,
                CountryCode = "NG",
                Name = "Education",
                Description = "Tuition and school fees",
                IconUrl = "https://cdn.aonik.io/catalog/icons/education.png",
                SortOrder = 4,
                IsActive = true
            },
            new()
            {
                Id = GovernmentCategoryId,
                TenantId = Guid.Empty,
                CountryCode = "KE",
                Name = "Government",
                Description = "Taxes and fees",
                IconUrl = "https://cdn.aonik.io/catalog/icons/government.png",
                SortOrder = 5,
                IsActive = true
            },
            new()
            {
                Id = CableCategoryId,
                TenantId = Guid.Empty,
                CountryCode = "KE",
                Name = "Cable",
                Description = "TV subscriptions",
                IconUrl = "https://cdn.aonik.io/catalog/icons/cable.png",
                SortOrder = 6,
                IsActive = true
            }
        };

        var existingIds = await _dbContext.CatalogBillerCategories
            .Select(category => category.Id)
            .ToListAsync(cancellationToken);

        var existingSet = new HashSet<Guid>(existingIds);
        var toAdd = categories.Where(category => !existingSet.Contains(category.Id)).ToList();

        if (toAdd.Count == 0)
        {
            return;
        }

        await _dbContext.CatalogBillerCategories.AddRangeAsync(toAdd, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Seeded {Count} biller categories", toAdd.Count);
    }
}
