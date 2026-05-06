using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Aonik.Finance.Entities.Catalog;
using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Finance.Services.Seeding.Phases;

/// <summary>
/// Seeds global biller categories (TenantId = Guid.Empty) from
/// the embedded finance-demo-catalog.json. Moved from CatalogSeedService.
/// </summary>
internal sealed class CatalogCategoriesSeedPhase
{
    private static readonly FinanceDemoSeedIds SeedIds = FinanceDemoSeedIds.Instance;

    private readonly FinanceDbContext _db;
    private readonly ILogger<CatalogCategoriesSeedPhase> _logger;

    public CatalogCategoriesSeedPhase(
        FinanceDbContext db,
        ILogger<CatalogCategoriesSeedPhase> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IReadOnlyList<string>> SeedAsync(CancellationToken cancellationToken)
    {
        var idByName = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase)
        {
            ["Utilities"]  = SeedIds.GlobalCategories.GlobalUtilitiesCategoryId,
            ["Telecom"]    = SeedIds.GlobalCategories.GlobalTelecomCategoryId,
            ["Internet"]   = SeedIds.GlobalCategories.GlobalInternetCategoryId,
            ["Education"]  = SeedIds.GlobalCategories.GlobalEducationCategoryId,
            ["Government"] = SeedIds.GlobalCategories.GlobalGovernmentCategoryId,
            ["Cable"]      = SeedIds.GlobalCategories.GlobalCableCategoryId,
        };

        var categories = FinanceDemoSeedCatalog.Instance.GlobalCategories
            .Select(record => new CatalogBillerCategory
            {
                Id = idByName[record.Name],
                TenantId = Guid.Empty,
                CountryCode = record.CountryCode,
                Name = record.Name,
                Description = record.Description,
                IconUrl = record.IconUrl,
                SortOrder = record.SortOrder,
                IsActive = true,
            })
            .ToList();

        var existingIds = await _db.CatalogBillerCategories
            .Select(category => category.Id)
            .ToListAsync(cancellationToken);

        var existingSet = new HashSet<Guid>(existingIds);
        var toAdd = categories.Where(category => !existingSet.Contains(category.Id)).ToList();

        if (toAdd.Count == 0)
        {
            return Array.Empty<string>();
        }

        await _db.CatalogBillerCategories.AddRangeAsync(toAdd, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Seeded {Count} biller categories", toAdd.Count);

        return new[] { $"Seeded {toAdd.Count} biller categories" };
    }
}
