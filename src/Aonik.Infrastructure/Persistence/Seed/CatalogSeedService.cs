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
        await SeedCustomerTiersAsync(cancellationToken);
        await SeedCategoriesAsync(cancellationToken);
        await SeedRelationshipTypesAsync(cancellationToken);
        await SeedOrderStatusesAsync(cancellationToken);
        await SeedOrderItemStatusesAsync(cancellationToken);
        await SeedPurposeCodesAsync(cancellationToken);
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

    private async Task SeedCustomerTiersAsync(CancellationToken cancellationToken)
    {
        var tiers = new List<ReferenceDataItem>
        {
            new()
            {
                Id = Guid.Parse("77777777-7777-7777-7777-777777777777"),
                Type = "CustomerTier",
                Code = "Retail",
                DisplayName = "Retail",
                SortOrder = 1,
                IsActive = true
            },
            new()
            {
                Id = Guid.Parse("88888888-8888-8888-8888-888888888888"),
                Type = "CustomerTier",
                Code = "SMB",
                DisplayName = "SMB",
                SortOrder = 2,
                IsActive = true
            },
            new()
            {
                Id = Guid.Parse("99999999-9999-9999-9999-999999999999"),
                Type = "CustomerTier",
                Code = "Enterprise",
                DisplayName = "Enterprise",
                SortOrder = 3,
                IsActive = true
            }
        };

        var existingKeys = await _dbContext.ReferenceDataItems
            .Where(item => item.Type == "CustomerTier")
            .Select(item => item.Code)
            .ToListAsync(cancellationToken);

        var existingSet = new HashSet<string>(existingKeys, StringComparer.OrdinalIgnoreCase);
        var toAdd = tiers.Where(tier => !existingSet.Contains(tier.Code)).ToList();

        if (toAdd.Count == 0)
        {
            return;
        }

        await _dbContext.ReferenceDataItems.AddRangeAsync(toAdd, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Seeded {Count} customer tiers", toAdd.Count);
    }

    private async Task SeedRelationshipTypesAsync(CancellationToken cancellationToken)
    {
        var types = new List<ReferenceDataItem>
        {
            new()
            {
                Id = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"),
                Type = "RelationshipType",
                Code = "Self",
                DisplayName = "Self",
                SortOrder = 1,
                IsActive = true
            },
            new()
            {
                Id = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002"),
                Type = "RelationshipType",
                Code = "Mother",
                DisplayName = "Mother",
                SortOrder = 2,
                IsActive = true
            },
            new()
            {
                Id = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000003"),
                Type = "RelationshipType",
                Code = "Father",
                DisplayName = "Father",
                SortOrder = 3,
                IsActive = true
            },
            new()
            {
                Id = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000004"),
                Type = "RelationshipType",
                Code = "Spouse",
                DisplayName = "Spouse",
                SortOrder = 4,
                IsActive = true
            },
            new()
            {
                Id = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000005"),
                Type = "RelationshipType",
                Code = "Sibling",
                DisplayName = "Sibling",
                SortOrder = 5,
                IsActive = true
            },
            new()
            {
                Id = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000006"),
                Type = "RelationshipType",
                Code = "Child",
                DisplayName = "Child",
                SortOrder = 6,
                IsActive = true
            },
            new()
            {
                Id = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000007"),
                Type = "RelationshipType",
                Code = "Friend",
                DisplayName = "Friend",
                SortOrder = 7,
                IsActive = true
            },
            new()
            {
                Id = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000008"),
                Type = "RelationshipType",
                Code = "Business",
                DisplayName = "Business",
                SortOrder = 8,
                IsActive = true
            },
            new()
            {
                Id = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000009"),
                Type = "RelationshipType",
                Code = "Other",
                DisplayName = "Other",
                SortOrder = 9,
                IsActive = true
            }
        };

        await SeedReferenceDataAsync(types, "RelationshipType", cancellationToken);
    }

    private async Task SeedOrderStatusesAsync(CancellationToken cancellationToken)
    {
        var statuses = new List<ReferenceDataItem>
        {
            BuildReferenceData("bbbbbbbb-0000-0000-0000-000000000001", "OrderStatus", "Draft", 1),
            BuildReferenceData("bbbbbbbb-0000-0000-0000-000000000002", "OrderStatus", "PendingSubmission", 2),
            BuildReferenceData("bbbbbbbb-0000-0000-0000-000000000003", "OrderStatus", "Submitted", 3),
            BuildReferenceData("bbbbbbbb-0000-0000-0000-000000000004", "OrderStatus", "PendingCompliance", 4),
            BuildReferenceData("bbbbbbbb-0000-0000-0000-000000000005", "OrderStatus", "Approved", 5),
            BuildReferenceData("bbbbbbbb-0000-0000-0000-000000000006", "OrderStatus", "PendingFunding", 6),
            BuildReferenceData("bbbbbbbb-0000-0000-0000-000000000007", "OrderStatus", "Funded", 7),
            BuildReferenceData("bbbbbbbb-0000-0000-0000-000000000008", "OrderStatus", "Processing", 8),
            BuildReferenceData("bbbbbbbb-0000-0000-0000-000000000009", "OrderStatus", "PartiallyCompleted", 9),
            BuildReferenceData("bbbbbbbb-0000-0000-0000-000000000010", "OrderStatus", "Completed", 10),
            BuildReferenceData("bbbbbbbb-0000-0000-0000-000000000011", "OrderStatus", "Failed", 11),
            BuildReferenceData("bbbbbbbb-0000-0000-0000-000000000012", "OrderStatus", "Cancelled", 12)
        };

        await SeedReferenceDataAsync(statuses, "OrderStatus", cancellationToken);
    }

    private async Task SeedOrderItemStatusesAsync(CancellationToken cancellationToken)
    {
        var statuses = new List<ReferenceDataItem>
        {
            BuildReferenceData("cccccccc-0000-0000-0000-000000000001", "OrderItemStatus", "Draft", 1),
            BuildReferenceData("cccccccc-0000-0000-0000-000000000002", "OrderItemStatus", "Valid", 2),
            BuildReferenceData("cccccccc-0000-0000-0000-000000000003", "OrderItemStatus", "QuoteExpired", 3),
            BuildReferenceData("cccccccc-0000-0000-0000-000000000004", "OrderItemStatus", "Invalid", 4),
            BuildReferenceData("cccccccc-0000-0000-0000-000000000005", "OrderItemStatus", "PendingPayout", 5),
            BuildReferenceData("cccccccc-0000-0000-0000-000000000006", "OrderItemStatus", "PayoutSubmitted", 6),
            BuildReferenceData("cccccccc-0000-0000-0000-000000000007", "OrderItemStatus", "Completed", 7),
            BuildReferenceData("cccccccc-0000-0000-0000-000000000008", "OrderItemStatus", "Failed", 8)
        };

        await SeedReferenceDataAsync(statuses, "OrderItemStatus", cancellationToken);
    }

    private async Task SeedPurposeCodesAsync(CancellationToken cancellationToken)
    {
        var purposes = new List<ReferenceDataItem>
        {
            BuildReferenceData("dddddddd-0000-0000-0000-000000000001", "PurposeCode", "Bills", 1),
            BuildReferenceData("dddddddd-0000-0000-0000-000000000002", "PurposeCode", "Utilities", 2),
            BuildReferenceData("dddddddd-0000-0000-0000-000000000003", "PurposeCode", "Education", 3),
            BuildReferenceData("dddddddd-0000-0000-0000-000000000004", "PurposeCode", "Telecom", 4),
            BuildReferenceData("dddddddd-0000-0000-0000-000000000005", "PurposeCode", "Other", 5)
        };

        await SeedReferenceDataAsync(purposes, "PurposeCode", cancellationToken);
    }

    private async Task SeedReferenceDataAsync(
        List<ReferenceDataItem> items,
        string type,
        CancellationToken cancellationToken)
    {
        var existingKeys = await _dbContext.ReferenceDataItems
            .Where(item => item.Type == type)
            .Select(item => item.Code)
            .ToListAsync(cancellationToken);

        var existingSet = new HashSet<string>(existingKeys, StringComparer.OrdinalIgnoreCase);
        var toAdd = items.Where(item => !existingSet.Contains(item.Code)).ToList();

        if (toAdd.Count == 0)
        {
            return;
        }

        await _dbContext.ReferenceDataItems.AddRangeAsync(toAdd, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Seeded {Count} {Type} reference data items", toAdd.Count, type);
    }

    private static ReferenceDataItem BuildReferenceData(string id, string type, string code, int sortOrder)
    {
        return new ReferenceDataItem
        {
            Id = Guid.Parse(id),
            Type = type,
            Code = code,
            DisplayName = code,
            SortOrder = sortOrder,
            IsActive = true
        };
    }
}
