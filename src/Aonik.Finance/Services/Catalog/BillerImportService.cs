using System.Text;
using System.Text.Json;

using Aonik.Finance.Contracts.Models.Catalog;
using Aonik.Finance.Contracts.Services.Catalog;
using Aonik.Finance.Contracts.Services.Partners.Connectors;
using Aonik.Finance.Entities.Catalog;
using Aonik.Finance.Entities.Partners;
using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Finance.Services.Catalog;

/// <summary>
/// Partner biller catalogue import (Spec 040 §8). Idempotent and upsert-based: the connector mapping is
/// the identity key, so re-running creates no duplicates; it refreshes mutable fields and
/// soft-deactivates services the partner has dropped (never hard-deletes — FKs are Restrict and any
/// orders/history must survive). Provider codes live only in <see cref="ConnectorBillerMapping"/>;
/// <see cref="CatalogBillerService.ServiceCode"/> stays an AONIK logical slug.
/// </summary>
internal sealed class BillerImportService : IBillerImportService
{
    private const string Ngn = "NGN";

    private readonly FinanceDbContext _dbContext;
    private readonly IPartnerConnectorResolver _connectorResolver;
    private readonly IEnumerable<IPartnerBillPaymentConnector> _billConnectors;
    private readonly IPermissionService _permissionService;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;

    public BillerImportService(
        FinanceDbContext dbContext,
        IPartnerConnectorResolver connectorResolver,
        IEnumerable<IPartnerBillPaymentConnector> billConnectors,
        IPermissionService permissionService,
        ICurrentUserProvider currentUserProvider,
        ITenantContext tenantContext,
        IClock clock)
    {
        _dbContext = dbContext;
        _connectorResolver = connectorResolver;
        _billConnectors = billConnectors;
        _permissionService = permissionService;
        _currentUserProvider = currentUserProvider;
        _tenantContext = tenantContext;
        _clock = clock;
    }

    // ── Sources ───────────────────────────────────────────────────────────────
    public async Task<BillerImportSourcesResponse> GetSourcesAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetCurrentTenantIdOrThrow();

        // The provider codes we actually have a bill-payment implementation for.
        var supported = _billConnectors
            .Select(c => c.ProviderCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var connectors = await _dbContext.Connectors.AsNoTracking()
            .Where(c => c.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        var sources = connectors
            .Where(c => supported.Contains(c.ConnectorType))
            .OrderBy(c => c.ConnectorType)
            .Select(c => new BillerImportSourceItem(
                c.Id,
                c.ConnectorType,
                c.Status,
                IsSandbox: string.Equals(c.ConnectorType, "Simulated", StringComparison.OrdinalIgnoreCase)))
            .ToList();

        return new BillerImportSourcesResponse(sources);
    }

    // ── Preview ───────────────────────────────────────────────────────────────
    public async Task<BillerImportPreviewResponse> PreviewAsync(
        BillerImportPreviewRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureWritePermissionAsync(cancellationToken);
        var tenantId = GetCurrentTenantIdOrThrow();
        var (_, billConnector) = await ResolveConnectorAsync(request.ConnectorId, tenantId, cancellationToken);

        // Dense read (no product expansion) — preview is cheap (O2).
        var query = new BillerCatalogQuery(request.CategoryCode, request.Country, Currency: null);
        var catalogue = await billConnector.GetBillerCatalogAsync(query, cancellationToken);

        var mappings = await _dbContext.ConnectorBillerMappings
            .Where(m => m.TenantId == tenantId && m.ConnectorId == request.ConnectorId)
            .ToListAsync(cancellationToken);

        // Biller-level mappings (ServiceId == null) keyed by provider biller code.
        var billerMappingByCode = new Dictionary<string, ConnectorBillerMapping>(StringComparer.OrdinalIgnoreCase);
        foreach (var mapping in mappings.Where(m => m.CatalogBillerServiceId == null))
        {
            billerMappingByCode[mapping.ProviderBillerCode] = mapping;
        }

        var billerIds = billerMappingByCode.Values.Select(m => m.CatalogBillerId).ToHashSet();
        var billersById = await _dbContext.CatalogBillers
            .Where(b => b.TenantId == tenantId && billerIds.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, cancellationToken);

        var entries = catalogue.Select(entry =>
        {
            string status;
            string? note = null;

            if (!billerMappingByCode.TryGetValue(entry.BillerCode, out var mapping))
            {
                status = "New";
            }
            else if (billersById.TryGetValue(mapping.CatalogBillerId, out var biller)
                     && !string.Equals(biller.Name, entry.BillerName, StringComparison.Ordinal))
            {
                status = "Changed";
                note = "name";
            }
            else
            {
                status = "Mapped";
            }

            return new BillerImportPreviewEntry(
                entry.BillerCode,
                entry.BillerName,
                entry.CategoryCode,
                entry.CategoryName,
                entry.ServiceCategory.ToString(),
                entry.Items.Count,
                status,
                note);
        }).ToList();

        return new BillerImportPreviewResponse(entries);
    }

    // ── Import (idempotent upsert) ────────────────────────────────────────────
    public async Task<BillerImportSummaryResponse> ImportAsync(
        BillerImportRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureWritePermissionAsync(cancellationToken);
        var tenantId = GetCurrentTenantIdOrThrow();
        var (connector, billConnector) = await ResolveConnectorAsync(request.ConnectorId, tenantId, cancellationToken);

        // Run-level dedupe of the selection (the provider may repeat codes).
        var selectedCodes = request.Entries
            .Where(e => !string.IsNullOrWhiteSpace(e.BillerCode))
            .Select(e => e.BillerCode.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var summary = new ImportCounters();
        if (selectedCodes.Count == 0)
        {
            return summary.ToResponse();
        }

        // Re-read authoritative data for the selected billers, expanding products. Field values come
        // from this fresh read, not from client-supplied entries (Spec 040 §8).
        var query = new BillerCatalogQuery(
            CategoryCode: null, Country: null, Currency: null,
            BillerCodes: selectedCodes, ExpandItems: true);
        var catalogue = await billConnector.GetBillerCatalogAsync(query, cancellationToken);

        var country = ResolveCountry(billConnector);
        var now = _clock.UtcNow;
        var userId = _currentUserProvider.GetCurrentUserId();
        var categoryCache = new Dictionary<string, CatalogBillerCategory>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in catalogue)
        {
            var category = await UpsertCategoryAsync(tenantId, entry, country, now, userId, categoryCache, cancellationToken);
            var biller = await UpsertBillerAsync(
                tenantId, request.ConnectorId, connector.PartnerId, entry, category.Id, country, now, userId, summary, cancellationToken);
            await UpsertServicesAsync(tenantId, request.ConnectorId, biller, entry, now, userId, summary, cancellationToken);
        }

        // Single transaction. Concurrent imports of the same connector are guarded by the mapping's
        // unique index (Spec 040 §8.3 / O1): the loser throws DbUpdateException, surfaced to the
        // operator, who can safely re-run (the upsert is idempotent).
        await _dbContext.SaveChangesAsync(cancellationToken);

        return summary.ToResponse();
    }

    // ── Upsert helpers ────────────────────────────────────────────────────────
    private async Task<CatalogBillerCategory> UpsertCategoryAsync(
        Guid tenantId, BillerCatalogEntry entry, string country, DateTime now, Guid? userId,
        Dictionary<string, CatalogBillerCategory> cache, CancellationToken cancellationToken)
    {
        var name = string.IsNullOrWhiteSpace(entry.CategoryName) ? "Bills" : entry.CategoryName.Trim();
        var cacheKey = $"{country}|{name.ToUpperInvariant()}";
        if (cache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var existing = await _dbContext.CatalogBillerCategories
            .FirstOrDefaultAsync(
                c => c.TenantId == tenantId && c.CountryCode == country && c.Name == name, cancellationToken);
        if (existing is not null)
        {
            cache[cacheKey] = existing;
            return existing;
        }

        var category = new CatalogBillerCategory
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CountryCode = country,
            Name = name,
            IsActive = true,
            CreatedAt = now,
            CreatedBy = userId
        };
        _dbContext.CatalogBillerCategories.Add(category);
        cache[cacheKey] = category;
        return category;
    }

    private async Task<CatalogBiller> UpsertBillerAsync(
        Guid tenantId, Guid connectorId, Guid partnerId, BillerCatalogEntry entry, Guid categoryId,
        string country, DateTime now, Guid? userId, ImportCounters summary, CancellationToken cancellationToken)
    {
        var billerMapping = await _dbContext.ConnectorBillerMappings.FirstOrDefaultAsync(
            m => m.TenantId == tenantId && m.ConnectorId == connectorId
                 && m.ProviderBillerCode == entry.BillerCode && m.CatalogBillerServiceId == null,
            cancellationToken);

        var existing = billerMapping is null
            ? null
            : await _dbContext.CatalogBillers.FirstOrDefaultAsync(b => b.Id == billerMapping.CatalogBillerId, cancellationToken);

        if (billerMapping is null || existing is null)
        {
            var biller = new CatalogBiller
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CategoryId = categoryId,
                CorrespondentPartnerId = partnerId,
                CountryCode = country,
                Name = entry.BillerName,
                IsActive = true,
                CreatedAt = now,
                CreatedBy = userId
            };
            _dbContext.CatalogBillers.Add(biller);

            if (billerMapping is null)
            {
                _dbContext.ConnectorBillerMappings.Add(new ConnectorBillerMapping
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    CatalogBillerId = biller.Id,
                    CatalogBillerServiceId = null,
                    ConnectorId = connectorId,
                    ProviderBillerCode = entry.BillerCode,
                    ProviderItemCode = null,
                    IsActive = true,
                    LastSyncedAt = now,
                    CreatedAt = now,
                    CreatedBy = userId
                });
            }
            else
            {
                // Mapping existed but its biller row is gone — repoint it.
                billerMapping.CatalogBillerId = biller.Id;
                billerMapping.IsActive = true;
                billerMapping.LastSyncedAt = now;
                billerMapping.UpdatedAt = now;
                billerMapping.UpdatedBy = userId;
            }

            summary.BillersCreated++;
            return biller;
        }

        existing.Name = entry.BillerName;
        existing.CategoryId = categoryId;
        existing.IsActive = true;
        existing.UpdatedAt = now;
        existing.UpdatedBy = userId;

        billerMapping.IsActive = true;
        billerMapping.LastSyncedAt = now;
        billerMapping.UpdatedAt = now;
        billerMapping.UpdatedBy = userId;

        summary.BillersUpdated++;
        return existing;
    }

    private async Task UpsertServicesAsync(
        Guid tenantId, Guid connectorId, CatalogBiller biller, BillerCatalogEntry entry,
        DateTime now, Guid? userId, ImportCounters summary, CancellationToken cancellationToken)
    {
        var fieldsJson = SerializeFields(entry.CustomerFields);
        var requiresValidation = entry.ServiceCategory == PartnerServiceCategory.BillPayment;
        var serviceType = entry.ServiceCategory.ToString();

        var existingServiceMappings = await _dbContext.ConnectorBillerMappings
            .Where(m => m.TenantId == tenantId && m.ConnectorId == connectorId
                        && m.CatalogBillerId == biller.Id && m.CatalogBillerServiceId != null)
            .ToListAsync(cancellationToken);

        var seenMappingIds = new HashSet<Guid>();

        foreach (var item in entry.Items)
        {
            var serviceMapping = existingServiceMappings.FirstOrDefault(
                m => string.Equals(m.ProviderItemCode, item.ItemCode, StringComparison.OrdinalIgnoreCase));

            var amountType = item.AmountType == BillAmountType.Fixed ? "Fixed" : "Variable";

            if (serviceMapping is null)
            {
                var service = new CatalogBillerService
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    BillerId = biller.Id,
                    ServiceCode = GenerateServiceCode(entry.BillerName, item.Name),
                    Name = item.Name,
                    Type = serviceType,
                    Currency = Ngn,
                    AmountType = amountType,
                    FixedAmount = item.FixedAmount?.Amount,
                    MinAmount = item.MinAmount?.Amount,
                    MaxAmount = item.MaxAmount?.Amount,
                    RequiresValidation = requiresValidation,
                    IsActive = true,
                    FieldsJson = fieldsJson,
                    CreatedAt = now,
                    CreatedBy = userId
                };
                _dbContext.CatalogBillerServices.Add(service);
                _dbContext.ConnectorBillerMappings.Add(new ConnectorBillerMapping
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    CatalogBillerId = biller.Id,
                    CatalogBillerServiceId = service.Id,
                    ConnectorId = connectorId,
                    ProviderBillerCode = entry.BillerCode,
                    ProviderItemCode = item.ItemCode,
                    IsActive = true,
                    LastSyncedAt = now,
                    CreatedAt = now,
                    CreatedBy = userId
                });
                summary.ServicesCreated++;
                continue;
            }

            seenMappingIds.Add(serviceMapping.Id);
            var existingService = await _dbContext.CatalogBillerServices
                .FirstOrDefaultAsync(s => s.Id == serviceMapping.CatalogBillerServiceId, cancellationToken);
            if (existingService is not null)
            {
                existingService.Name = item.Name;
                existingService.Type = serviceType;
                existingService.AmountType = amountType;
                existingService.FixedAmount = item.FixedAmount?.Amount;
                existingService.MinAmount = item.MinAmount?.Amount;
                existingService.MaxAmount = item.MaxAmount?.Amount;
                existingService.RequiresValidation = requiresValidation;
                existingService.FieldsJson = fieldsJson;
                existingService.IsActive = true;
                existingService.UpdatedAt = now;
                existingService.UpdatedBy = userId;
            }

            serviceMapping.IsActive = true;
            serviceMapping.LastSyncedAt = now;
            serviceMapping.UpdatedAt = now;
            serviceMapping.UpdatedBy = userId;
            summary.ServicesUpdated++;
        }

        // Soft-deactivate services this connector no longer offers for this biller.
        foreach (var stale in existingServiceMappings.Where(m => !seenMappingIds.Contains(m.Id) && m.IsActive))
        {
            stale.IsActive = false;
            stale.UpdatedAt = now;
            stale.UpdatedBy = userId;

            var staleService = await _dbContext.CatalogBillerServices
                .FirstOrDefaultAsync(s => s.Id == stale.CatalogBillerServiceId, cancellationToken);
            if (staleService is not null)
            {
                staleService.IsActive = false;
                staleService.UpdatedAt = now;
                staleService.UpdatedBy = userId;
            }

            summary.Deactivated++;
        }
    }

    // ── Shared helpers ────────────────────────────────────────────────────────
    private async Task<(Connector Connector, IPartnerBillPaymentConnector BillConnector)> ResolveConnectorAsync(
        Guid connectorId, Guid tenantId, CancellationToken cancellationToken)
    {
        var connector = await _dbContext.Connectors
            .FirstOrDefaultAsync(c => c.Id == connectorId && c.TenantId == tenantId, cancellationToken);
        if (connector is null)
        {
            throw new InvalidOperationException($"Connector {connectorId} not found.");
        }

        var billConnector = _connectorResolver.ResolveBillPaymentConnector(connector.ConnectorType);
        return (connector, billConnector);
    }

    private static string ResolveCountry(IPartnerBillPaymentConnector connector)
        => connector.Capabilities.SelectMany(c => c.Countries).FirstOrDefault() ?? "NG";

    private static string SerializeFields(IReadOnlyList<BillCustomerField> fields)
    {
        var modelFields = fields
            .Select(f => new CatalogServiceField(f.Key, f.Label, "text", f.Required, null, null, null, null, null))
            .ToList();
        return JsonSerializer.Serialize(modelFields);
    }

    /// <summary>
    /// AONIK-owned logical service code — a slug of the biller + item NAMES, never the vendor code
    /// (Spec 040 §7). Identity on re-import comes from the mapping's provider codes, not this field.
    /// </summary>
    private static string GenerateServiceCode(string billerName, string itemName)
    {
        var biller = Slug(billerName);
        var item = Slug(itemName);
        var combined = string.IsNullOrEmpty(item) ? biller : $"{biller}-{item}";
        return string.IsNullOrEmpty(combined) ? "service" : combined;
    }

    private static string Slug(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        var lastWasDash = false;
        foreach (var ch in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
                lastWasDash = false;
            }
            else if (!lastWasDash)
            {
                builder.Append('-');
                lastWasDash = true;
            }
        }

        return builder.ToString().Trim('-');
    }

    private async Task EnsureWritePermissionAsync(CancellationToken cancellationToken)
    {
        var userId = _currentUserProvider.GetCurrentUserId();
        if (!userId.HasValue)
        {
            throw new PermissionDeniedException("Catalog.Write", "Authenticated user is required.");
        }

        var hasPermission = await _permissionService.HasPermissionAsync(userId.Value, "Catalog.Write", cancellationToken);
        if (!hasPermission)
        {
            throw new PermissionDeniedException("Catalog.Write");
        }
    }

    private Guid GetCurrentTenantIdOrThrow()
    {
        var tenantId = _tenantContext.TenantId;
        if (tenantId is null || tenantId.Value == Guid.Empty)
        {
            throw new InvalidOperationException("A tenant context is required for catalog import.");
        }

        return tenantId.Value;
    }

    private sealed class ImportCounters
    {
        public int BillersCreated { get; set; }
        public int BillersUpdated { get; set; }
        public int ServicesCreated { get; set; }
        public int ServicesUpdated { get; set; }
        public int Deactivated { get; set; }

        public BillerImportSummaryResponse ToResponse()
            => new(BillersCreated, BillersUpdated, ServicesCreated, ServicesUpdated, Deactivated);
    }
}
