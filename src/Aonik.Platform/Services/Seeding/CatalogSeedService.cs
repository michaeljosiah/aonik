using System.Reflection;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Aonik.Finance.Entities.Catalog;
using Aonik.Finance.Persistence;
using Aonik.Platform.Entities.Party;
using Aonik.Platform.Entities.ReferenceData;
using Aonik.Platform.Persistence;

namespace Aonik.Platform.Services.Seeding;

internal class CatalogSeedService
{
    private static readonly Guid UtilitiesCategoryId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TelecomCategoryId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid InternetCategoryId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid EducationCategoryId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid GovernmentCategoryId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid CableCategoryId = Guid.Parse("66666666-6666-6666-6666-666666666666");

    private readonly PlatformDbContext _platformDbContext;
    private readonly FinanceDbContext _financeDbContext;
    private readonly ILogger<CatalogSeedService> _logger;

    public CatalogSeedService(PlatformDbContext platformDbContext, FinanceDbContext financeDbContext, ILogger<CatalogSeedService> logger)
    {
        _platformDbContext = platformDbContext;
        _financeDbContext = financeDbContext;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await SeedCountriesAsync(cancellationToken);
        await SeedCurrenciesAsync(cancellationToken);
        await SeedCountryCurrenciesAsync(cancellationToken);
        await SeedCustomerTiersAsync(cancellationToken);
        await SeedCategoriesAsync(cancellationToken);
        await SeedRelationshipTypesAsync(cancellationToken);
        await SeedOrderStatusesAsync(cancellationToken);
        await SeedOrderItemStatusesAsync(cancellationToken);
        await SeedPurposeCodesAsync(cancellationToken);
    }

    private async Task SeedCountriesAsync(CancellationToken cancellationToken)
    {
        var records = ReadEmbeddedJson<List<CountrySeedRecord>>("Aonik.Platform.Persistence.Seed.Data.countries.derived.world-countries-json.json");

        var existing = await _platformDbContext.Countries
            .Where(x => x.TenantId == null)
            .ToListAsync(cancellationToken);

        var existingByCode = existing
            .GroupBy(x => x.IsoAlpha2, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToDictionary(x => x.IsoAlpha2, x => x, StringComparer.OrdinalIgnoreCase);

        var toAdd = new List<Country>();
        var updated = 0;

        for (var i = 0; i < records.Count; i++)
        {
            var record = records[i];
            var code = record.IsoAlpha2.Trim().ToUpperInvariant();
            var name = record.Name.Trim();
            var alpha3 = record.IsoAlpha3.Trim().ToUpperInvariant();

            if (existingByCode.TryGetValue(code, out var existingItem))
            {
                var changed = false;

                if (!string.Equals(existingItem.Name, name, StringComparison.Ordinal))
                {
                    existingItem.Name = name;
                    changed = true;
                }

                if (!string.Equals(existingItem.IsoAlpha3, alpha3, StringComparison.Ordinal))
                {
                    existingItem.IsoAlpha3 = alpha3;
                    changed = true;
                }

                if (existingItem.IsoNumeric != record.IsoNumeric)
                {
                    existingItem.IsoNumeric = record.IsoNumeric;
                    changed = true;
                }

                var sortOrder = i + 1;
                if (existingItem.SortOrder != sortOrder)
                {
                    existingItem.SortOrder = sortOrder;
                    changed = true;
                }

                if (!existingItem.IsActive)
                {
                    existingItem.IsActive = true;
                    changed = true;
                }

                if (changed)
                    updated++;

                continue;
            }

            toAdd.Add(new Country
            {
                Id = Guid.NewGuid(),
                TenantId = null,
                IsoAlpha2 = code,
                IsoAlpha3 = alpha3,
                IsoNumeric = record.IsoNumeric,
                Name = name,
                SortOrder = i + 1,
                IsActive = true
            });
        }

        if (toAdd.Count == 0 && updated == 0)
            return;

        if (toAdd.Count > 0)
            await _platformDbContext.Countries.AddRangeAsync(toAdd, cancellationToken);

        await _platformDbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Seeded {Count} countries (added {Added}, updated {Updated})", toAdd.Count + updated, toAdd.Count, updated);
    }

    private async Task SeedCurrenciesAsync(CancellationToken cancellationToken)
    {
        var records = ReadEmbeddedJson<List<CurrencySeedRecord>>("Aonik.Platform.Persistence.Seed.Data.currencies.iso4217.canonical.json");

        var existing = await _platformDbContext.Currencies
            .Where(x => x.TenantId == null)
            .ToListAsync(cancellationToken);

        var existingByCode = existing
            .GroupBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToDictionary(x => x.Code, x => x, StringComparer.OrdinalIgnoreCase);

        var toAdd = new List<Currency>();
        var updated = 0;

        for (var i = 0; i < records.Count; i++)
        {
            var record = records[i];
            var code = record.Code.Trim().ToUpperInvariant();
            var name = record.Name.Trim();

            var isActive = string.IsNullOrEmpty(record.WithdrawalDate);
            var sortOrder = i + 1;

            if (existingByCode.TryGetValue(code, out var existingItem))
            {
                var changed = false;

                if (!string.Equals(existingItem.Name, name, StringComparison.Ordinal))
                {
                    existingItem.Name = name;
                    changed = true;
                }

                if (!string.Equals(existingItem.NumericCode, record.NumericCode, StringComparison.Ordinal))
                {
                    existingItem.NumericCode = record.NumericCode;
                    changed = true;
                }

                if (existingItem.MinorUnit != record.MinorUnit)
                {
                    existingItem.MinorUnit = record.MinorUnit;
                    changed = true;
                }

                if (existingItem.WithdrawalDate != record.WithdrawalDate)
                {
                    existingItem.WithdrawalDate = record.WithdrawalDate;
                    changed = true;
                }

                if (existingItem.SortOrder != sortOrder)
                {
                    existingItem.SortOrder = sortOrder;
                    changed = true;
                }

                if (existingItem.IsActive != isActive)
                {
                    existingItem.IsActive = isActive;
                    changed = true;
                }

                if (changed)
                    updated++;

                continue;
            }

            toAdd.Add(new Currency
            {
                Id = Guid.NewGuid(),
                TenantId = null,
                Code = code,
                Name = name,
                NumericCode = record.NumericCode,
                MinorUnit = record.MinorUnit,
                WithdrawalDate = record.WithdrawalDate,
                SortOrder = sortOrder,
                IsActive = isActive
            });
        }

        if (toAdd.Count == 0 && updated == 0)
            return;

        if (toAdd.Count > 0)
            await _platformDbContext.Currencies.AddRangeAsync(toAdd, cancellationToken);

        await _platformDbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Seeded {Count} currencies (added {Added}, updated {Updated})", toAdd.Count + updated, toAdd.Count, updated);
    }

    private async Task SeedCountryCurrenciesAsync(CancellationToken cancellationToken)
    {
        var countries = await _platformDbContext.Countries
            .Where(x => x.TenantId == null)
            .ToListAsync(cancellationToken);

        var existingMappings = await _platformDbContext.CountryCurrencies
            .ToListAsync(cancellationToken);

        var countryIdByCode = countries.ToDictionary(x => x.IsoAlpha2, x => x.Id, StringComparer.OrdinalIgnoreCase);

        var defaultCurrencies = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["AF"] = "AFN", ["AL"] = "ALL", ["DZ"] = "DZD", ["AS"] = "USD", ["AD"] = "EUR",
            ["AO"] = "AOA", ["AI"] = "XCD", ["AQ"] = "", ["AG"] = "XCD", ["AR"] = "ARS",
            ["AM"] = "AMD", ["AW"] = "AWG", ["AU"] = "AUD", ["AT"] = "EUR", ["AZ"] = "AZN",
            ["BS"] = "BSD", ["BH"] = "BHD", ["BD"] = "BDT", ["BB"] = "BBD", ["BY"] = "BYN",
            ["BE"] = "EUR", ["BZ"] = "BZD", ["BJ"] = "XOF", ["BM"] = "BMD", ["BT"] = "BTN",
            ["BO"] = "BOB", ["BQ"] = "USD", ["BA"] = "BAM", ["BW"] = "BWP", ["BV"] = "NOK",
            ["BR"] = "BRL", ["IO"] = "USD", ["BN"] = "BND", ["BG"] = "BGN", ["BF"] = "XOF",
            ["BI"] = "BIF", ["CV"] = "CVE", ["KH"] = "KHR", ["CM"] = "XAF", ["CA"] = "CAD",
            ["KY"] = "KYD", ["CF"] = "XAF", ["TD"] = "XAF", ["CL"] = "CLP", ["CN"] = "CNY",
            ["CX"] = "AUD", ["CC"] = "AUD", ["CO"] = "COP", ["KM"] = "KMF", ["CG"] = "XAF",
            ["CD"] = "CDF", ["CK"] = "NZD", ["CR"] = "CRC", ["HR"] = "EUR", ["CU"] = "CUP",
            ["CW"] = "ANG", ["CY"] = "EUR", ["CZ"] = "CZK", ["DK"] = "DKK", ["DJ"] = "DJF",
            ["DM"] = "XCD", ["DO"] = "DOP", ["EC"] = "USD", ["EG"] = "EGP", ["SV"] = "USD",
            ["GQ"] = "XAF", ["ER"] = "ERN", ["EE"] = "EUR", ["SZ"] = "SZL", ["ET"] = "ETB",
            ["FK"] = "FKP", ["FO"] = "DKK", ["FJ"] = "FJD", ["FI"] = "EUR", ["FR"] = "EUR",
            ["GF"] = "EUR", ["PF"] = "XPF", ["TF"] = "EUR", ["GA"] = "XAF", ["GM"] = "GMD",
            ["GE"] = "GEL", ["DE"] = "EUR", ["GH"] = "GHS", ["GI"] = "GIP", ["GR"] = "EUR",
            ["GL"] = "DKK", ["GD"] = "XCD", ["GP"] = "EUR", ["GU"] = "USD", ["GT"] = "GTQ",
            ["GG"] = "GBP", ["GN"] = "GNF", ["GW"] = "XOF", ["GY"] = "GYD", ["HT"] = "HTG",
            ["HM"] = "AUD", ["VA"] = "EUR", ["HN"] = "HNL", ["HK"] = "HKD", ["HU"] = "HUF",
            ["IS"] = "ISK", ["IN"] = "INR", ["ID"] = "IDR", ["IR"] = "IRR", ["IQ"] = "IQD",
            ["IE"] = "EUR", ["IM"] = "GBP", ["IL"] = "ILS", ["IT"] = "EUR", ["JM"] = "JMD",
            ["JP"] = "JPY", ["JE"] = "GBP", ["JO"] = "JOD", ["KZ"] = "KZT", ["KE"] = "KES",
            ["KI"] = "AUD", ["KP"] = "KPW", ["KR"] = "KRW", ["KW"] = "KWD", ["KG"] = "KGS",
            ["LA"] = "LAK", ["LV"] = "EUR", ["LB"] = "LBP", ["LS"] = "LSL", ["LR"] = "LRD",
            ["LY"] = "LYD", ["LI"] = "CHF", ["LT"] = "EUR", ["LU"] = "EUR", ["MO"] = "MOP",
            ["MG"] = "MGA", ["MW"] = "MWK", ["MY"] = "MYR", ["MV"] = "MVR", ["ML"] = "XOF",
            ["MT"] = "EUR", ["MH"] = "USD", ["MQ"] = "EUR", ["MR"] = "MRU", ["MU"] = "MUR",
            ["YT"] = "EUR", ["MX"] = "MXN", ["FM"] = "USD", ["MD"] = "MDL", ["MC"] = "EUR",
            ["MN"] = "MNT", ["ME"] = "EUR", ["MS"] = "XCD", ["MA"] = "MAD", ["MZ"] = "MZN",
            ["MM"] = "MMK", ["NA"] = "NAD", ["NR"] = "AUD", ["NP"] = "NPR", ["NL"] = "EUR",
            ["NC"] = "XPF", ["NZ"] = "NZD", ["NI"] = "NIO", ["NE"] = "XOF", ["NG"] = "NGN",
            ["NU"] = "NZD", ["NF"] = "AUD", ["MK"] = "MKD", ["MP"] = "USD", ["NO"] = "NOK",
            ["OM"] = "OMR", ["PK"] = "PKR", ["PW"] = "USD", ["PS"] = "ILS", ["PA"] = "PAB",
            ["PG"] = "PGK", ["PY"] = "PYG", ["PE"] = "PEN", ["PH"] = "PHP", ["PN"] = "NZD",
            ["PL"] = "PLN", ["PT"] = "EUR", ["PR"] = "USD", ["QA"] = "QAR", ["RE"] = "EUR",
            ["RO"] = "RON", ["RU"] = "RUB", ["RW"] = "RWF", ["BL"] = "EUR", ["SH"] = "SHP",
            ["KN"] = "XCD", ["LC"] = "XCD", ["MF"] = "EUR", ["PM"] = "EUR", ["VC"] = "XCD",
            ["WS"] = "WST", ["SM"] = "EUR", ["ST"] = "STN", ["SA"] = "SAR", ["SN"] = "XOF",
            ["RS"] = "RSD", ["SC"] = "SCR", ["SL"] = "SLE", ["SG"] = "SGD", ["SX"] = "ANG",
            ["SK"] = "EUR", ["SI"] = "EUR", ["SB"] = "SBD", ["SO"] = "SOS", ["ZA"] = "ZAR",
            ["GS"] = "", ["SS"] = "SSP", ["ES"] = "EUR", ["LK"] = "LKR", ["SD"] = "SDG",
            ["SR"] = "SRD", ["SJ"] = "NOK", ["SE"] = "SEK", ["CH"] = "CHF", ["SY"] = "SYP",
            ["TW"] = "TWD", ["TJ"] = "TJS", ["TZ"] = "TZS", ["TH"] = "THB", ["TL"] = "USD",
            ["TG"] = "XOF", ["TK"] = "NZD", ["TO"] = "TOP", ["TT"] = "TTD", ["TN"] = "TND",
            ["TR"] = "TRY", ["TM"] = "TMT", ["TC"] = "USD", ["TV"] = "AUD", ["UG"] = "UGX",
            ["UA"] = "UAH", ["AE"] = "AED", ["GB"] = "GBP", ["US"] = "USD", ["UM"] = "USD",
            ["UY"] = "UYU", ["UZ"] = "UZS", ["VU"] = "VUV", ["VE"] = "VES", ["VN"] = "VND",
            ["VG"] = "USD", ["VI"] = "USD", ["WF"] = "XPF", ["EH"] = "MAD", ["YE"] = "YER",
            ["ZM"] = "ZMW", ["ZW"] = "ZWL"
        };

        var toAdd = new List<CountryCurrency>();

        foreach (var kvp in defaultCurrencies)
        {
            var countryCode = kvp.Key;
            var currencyCode = kvp.Value;

            if (string.IsNullOrEmpty(currencyCode))
                continue;

            if (!countryIdByCode.TryGetValue(countryCode, out var countryId))
                continue;

            var alreadyExists = existingMappings.Any(x =>
                x.CountryId == countryId &&
                string.Equals(x.CurrencyCode, currencyCode, StringComparison.OrdinalIgnoreCase));

            if (alreadyExists)
                continue;

            toAdd.Add(new CountryCurrency
            {
                Id = Guid.NewGuid(),
                CountryId = countryId,
                CurrencyCode = currencyCode.ToUpperInvariant(),
                IsDefault = true
            });
        }

        if (toAdd.Count == 0)
            return;

        await _platformDbContext.CountryCurrencies.AddRangeAsync(toAdd, cancellationToken);
        await _platformDbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Seeded {Count} country-currency mappings", toAdd.Count);
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

        var existingIds = await _financeDbContext.CatalogBillerCategories
            .Select(category => category.Id)
            .ToListAsync(cancellationToken);

        var existingSet = new HashSet<Guid>(existingIds);
        var toAdd = categories.Where(category => !existingSet.Contains(category.Id)).ToList();

        if (toAdd.Count == 0)
        {
            return;
        }

        await _financeDbContext.CatalogBillerCategories.AddRangeAsync(toAdd, cancellationToken);
        await _financeDbContext.SaveChangesAsync(cancellationToken);
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

        var existingKeys = await _platformDbContext.ReferenceDataItems
            .Where(item => item.Type == "CustomerTier")
            .Select(item => item.Code)
            .ToListAsync(cancellationToken);

        var existingSet = new HashSet<string>(existingKeys, StringComparer.OrdinalIgnoreCase);
        var toAdd = tiers.Where(tier => !existingSet.Contains(tier.Code)).ToList();

        if (toAdd.Count == 0)
        {
            return;
        }

        await _platformDbContext.ReferenceDataItems.AddRangeAsync(toAdd, cancellationToken);
        await _platformDbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Seeded {Count} customer tiers", toAdd.Count);
    }

    private async Task SeedRelationshipTypesAsync(CancellationToken cancellationToken)
    {
        var types = PartyRelationshipTypes.All
            .Select(type => new ReferenceDataItem
            {
                Id = Guid.Parse(RelationshipTypeId(type.SortOrder)),
                Type = "RelationshipType",
                Code = type.Code,
                DisplayName = type.DisplayName,
                SortOrder = type.SortOrder,
                IsActive = true
            })
            .ToList();

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
        var existingKeys = await _platformDbContext.ReferenceDataItems
            .Where(item => item.Type == type)
            .Select(item => item.Code)
            .ToListAsync(cancellationToken);

        var existingSet = new HashSet<string>(existingKeys, StringComparer.OrdinalIgnoreCase);
        var toAdd = items.Where(item => !existingSet.Contains(item.Code)).ToList();

        if (toAdd.Count == 0)
        {
            return;
        }

        await _platformDbContext.ReferenceDataItems.AddRangeAsync(toAdd, cancellationToken);
        await _platformDbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Seeded {Count} {Type} reference data items", toAdd.Count, type);
    }

    private static T ReadEmbeddedJson<T>(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");
        }

        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
        return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
               ?? throw new InvalidOperationException($"Failed to deserialize embedded resource '{resourceName}'.");
    }

    private sealed record CountrySeedRecord(string IsoAlpha2, string IsoAlpha3, int? IsoNumeric, string Name);
    private sealed record CurrencySeedRecord(string Code, string Name, string? NumericCode, int? MinorUnit, string? WithdrawalDate);

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

    private static string RelationshipTypeId(int sortOrder)
    {
        return $"aaaaaaaa-0000-0000-0000-{sortOrder:000000000000}";
    }
}
