using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Aonik.Application.Abstractions.Multitenancy;
using Aonik.Application.Abstractions.Persistence;
using Aonik.Application.Models.Identity;
using Aonik.Application.Models.Partners;
using Aonik.Application.Services.Compliance;
using Aonik.Application.Services.Identity;
using Aonik.Domain.Ledger.Entities;
using Aonik.Domain.Partners.Entities;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Application.Services.Partners;

public class PartnerAdminService : AdminServiceBase, IPartnerAdminService
{
    private const string PrefundAccountRole = "PrefundAsset";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IAonikDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IClock _clock;
    private readonly IAuditLogWriter _auditLogWriter;

    public PartnerAdminService(
        IAonikDbContext dbContext,
        ITenantProvider tenantProvider,
        IClock clock,
        IAuditLogWriter auditLogWriter,
        ICurrentUserProvider currentUserProvider,
        IPermissionService permissionService)
        : base(currentUserProvider, permissionService)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _clock = clock;
        _auditLogWriter = auditLogWriter;
    }

    public async Task<PagedResult<PartnerListItem>> ListPartnersAsync(
        ListPartnersRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Catalog.Read", cancellationToken);

        var tenantId = _tenantProvider.GetCurrentTenantId();
        var pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;
        var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;

        var query = _dbContext.Partners
            .AsNoTracking()
            .Where(partner => partner.TenantId == tenantId)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim();
            query = query.Where(partner => partner.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(partner => partner.Name.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(request.CountryCode))
        {
            var countryCode = request.CountryCode.Trim();
            query = query.Where(partner => _dbContext.PartnerBranches
                .Any(branch =>
                    branch.TenantId == tenantId &&
                    branch.PartnerId == partner.Id &&
                    branch.Country == countryCode));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var partners = await query
            .OrderBy(partner => partner.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(partner => new
            {
                partner.Id,
                partner.Name,
                partner.Status,
                partner.CreatedAt,
                partner.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        if (partners.Count == 0)
        {
            return new PagedResult<PartnerListItem>(
                new List<PartnerListItem>(),
                totalCount,
                pageNumber,
                pageSize);
        }

        var partnerIds = partners.Select(partner => partner.Id).ToList();

        var branchRows = await _dbContext.PartnerBranches
            .AsNoTracking()
            .Where(branch => branch.TenantId == tenantId && partnerIds.Contains(branch.PartnerId))
            .Select(branch => new
            {
                branch.PartnerId,
                branch.Country
            })
            .ToListAsync(cancellationToken);

        var branchStats = branchRows
            .GroupBy(branch => branch.PartnerId)
            .ToDictionary(
                group => group.Key,
                group => new
                {
                    BranchCount = group.Count(),
                    CoverageCountries = group
                        .Select(item => item.Country)
                        .Where(country => !string.IsNullOrWhiteSpace(country))
                        .Distinct()
                        .OrderBy(country => country)
                        .ToList()
                });

        var connectorCounts = await _dbContext.Connectors
            .AsNoTracking()
            .Where(connector => connector.TenantId == tenantId && partnerIds.Contains(connector.PartnerId))
            .GroupBy(connector => connector.PartnerId)
            .Select(group => new
            {
                PartnerId = group.Key,
                Count = group.Count()
            })
            .ToDictionaryAsync(item => item.PartnerId, item => item.Count, cancellationToken);

        var activeRoutingRuleCounts = await _dbContext.RoutingRules
            .AsNoTracking()
            .Where(rule =>
                rule.TenantId == tenantId &&
                rule.IsActive &&
                rule.TargetPartnerId.HasValue &&
                partnerIds.Contains(rule.TargetPartnerId.Value))
            .GroupBy(rule => rule.TargetPartnerId!.Value)
            .Select(group => new
            {
                PartnerId = group.Key,
                Count = group.Count()
            })
            .ToDictionaryAsync(item => item.PartnerId, item => item.Count, cancellationToken);

        var linkedBillerCounts = await _dbContext.CatalogBillers
            .AsNoTracking()
            .Where(biller =>
                biller.TenantId == tenantId &&
                partnerIds.Contains(biller.CorrespondentPartnerId))
            .GroupBy(biller => biller.CorrespondentPartnerId)
            .Select(group => new
            {
                PartnerId = group.Key,
                Count = group.Count()
            })
            .ToDictionaryAsync(item => item.PartnerId, item => item.Count, cancellationToken);

        var items = partners.Select(partner =>
        {
            branchStats.TryGetValue(partner.Id, out var branchInfo);
            connectorCounts.TryGetValue(partner.Id, out var connectorCount);
            activeRoutingRuleCounts.TryGetValue(partner.Id, out var activeRoutingRuleCount);
            linkedBillerCounts.TryGetValue(partner.Id, out var linkedBillerCount);

            return new PartnerListItem(
                partner.Id,
                partner.Name,
                partner.Status,
                branchInfo?.BranchCount ?? 0,
                connectorCount,
                activeRoutingRuleCount,
                linkedBillerCount,
                branchInfo?.CoverageCountries ?? new List<string>(),
                partner.CreatedAt,
                partner.UpdatedAt);
        }).ToList();

        return new PagedResult<PartnerListItem>(
            items,
            totalCount,
            pageNumber,
            pageSize);
    }

    public async Task<PartnerDetail?> GetPartnerAsync(
        Guid partnerId,
        CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Catalog.Read", cancellationToken);

        var tenantId = _tenantProvider.GetCurrentTenantId();

        var partner = await _dbContext.Partners
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.Id == partnerId)
            .Select(item => new
            {
                item.Id,
                item.Name,
                item.Status,
                item.CapabilitiesJson,
                item.OperatingHoursJson,
                item.CreatedAt,
                item.UpdatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (partner == null)
        {
            return null;
        }

        var branches = await _dbContext.PartnerBranches
            .AsNoTracking()
            .Where(branch => branch.TenantId == tenantId && branch.PartnerId == partnerId)
            .OrderBy(branch => branch.Country)
            .ThenBy(branch => branch.City)
            .ThenBy(branch => branch.Name)
            .Select(branch => new PartnerBranchItem(
                branch.Id,
                branch.Name,
                branch.Country,
                branch.City,
                branch.MetadataJson,
                branch.CreatedAt,
                branch.UpdatedAt))
            .ToListAsync(cancellationToken);

        var connectors = await _dbContext.Connectors
            .AsNoTracking()
            .Where(connector => connector.TenantId == tenantId && connector.PartnerId == partnerId)
            .OrderBy(connector => connector.ConnectorType)
            .ThenBy(connector => connector.CreatedAt)
            .Select(connector => new PartnerConnectorItem(
                connector.Id,
                connector.ConnectorType,
                connector.Status,
                connector.CredentialsRef,
                connector.CreatedAt,
                connector.UpdatedAt))
            .ToListAsync(cancellationToken);

        var connectorIds = connectors
            .Select(connector => connector.ConnectorId)
            .ToList();

        var routingRulesQuery = _dbContext.RoutingRules
            .AsNoTracking()
            .Where(rule =>
                rule.TenantId == tenantId &&
                (rule.TargetPartnerId == partnerId ||
                 (rule.TargetConnectorId.HasValue && connectorIds.Contains(rule.TargetConnectorId.Value))));

        var routingRules = await routingRulesQuery
            .OrderBy(rule => rule.Priority)
            .ThenByDescending(rule => rule.IsActive)
            .Select(rule => new PartnerRoutingRuleItem(
                rule.Id,
                rule.Priority,
                rule.IsActive,
                rule.ConditionsJson,
                rule.TargetConnectorId,
                rule.CreatedAt,
                rule.UpdatedAt))
            .ToListAsync(cancellationToken);

        var connectorTypesById = connectors.ToDictionary(connector => connector.ConnectorId, connector => connector.ConnectorType);

        var recentTransmissions = connectorIds.Count == 0
            ? new List<PartnerTransmissionItem>()
            : await _dbContext.Transmissions
                .AsNoTracking()
                .Where(transmission => transmission.TenantId == tenantId && connectorIds.Contains(transmission.ConnectorId))
                .OrderByDescending(transmission => transmission.CreatedAt)
                .Take(20)
                .Select(transmission => new PartnerTransmissionItem(
                    transmission.Id,
                    transmission.ConnectorId,
                    null,
                    transmission.Status,
                    transmission.RetryCount,
                    transmission.LastError,
                    transmission.CreatedAt,
                    transmission.UpdatedAt))
                .ToListAsync(cancellationToken);

        var transmissionsWithTypes = recentTransmissions
            .Select(transmission => new PartnerTransmissionItem(
                transmission.TransmissionId,
                transmission.ConnectorId,
                connectorTypesById.TryGetValue(transmission.ConnectorId, out var connectorType) ? connectorType : null,
                transmission.Status,
                transmission.RetryCount,
                transmission.LastError,
                transmission.CreatedAt,
                transmission.UpdatedAt))
            .ToList();

        var linkedBillers = await _dbContext.CatalogBillers
            .AsNoTracking()
            .Where(biller => biller.TenantId == tenantId && biller.CorrespondentPartnerId == partnerId)
            .OrderBy(biller => biller.Name)
            .Select(biller => new
            {
                biller.Id,
                biller.Name,
                biller.CountryCode,
                biller.IsActive
            })
            .ToListAsync(cancellationToken);

        var linkedBillerIds = linkedBillers.Select(biller => biller.Id).ToList();

        var serviceCounts = linkedBillerIds.Count == 0
            ? new Dictionary<Guid, int>()
            : await _dbContext.CatalogBillerServices
                .AsNoTracking()
                .Where(service => linkedBillerIds.Contains(service.BillerId))
                .GroupBy(service => service.BillerId)
                .Select(group => new
                {
                    BillerId = group.Key,
                    Count = group.Count()
                })
                .ToDictionaryAsync(item => item.BillerId, item => item.Count, cancellationToken);

        var linkedBillerItems = linkedBillers
            .Select(biller => new PartnerLinkedBillerItem(
                biller.Id,
                biller.Name,
                biller.CountryCode,
                biller.IsActive,
                serviceCounts.TryGetValue(biller.Id, out var serviceCount) ? serviceCount : 0))
            .ToList();

        var activeRoutingRuleCount = routingRules.Count(rule => rule.IsActive);

        return new PartnerDetail(
            partner.Id,
            partner.Name,
            partner.Status,
            partner.CapabilitiesJson,
            partner.OperatingHoursJson,
            partner.CreatedAt,
            partner.UpdatedAt,
            branches.Count,
            connectors.Count,
            activeRoutingRuleCount,
            linkedBillerItems.Count,
            branches,
            connectors,
            routingRules,
            transmissionsWithTypes,
            linkedBillerItems);
    }

    public async Task<CreatePartnerResponse> CreatePartnerAsync(
        CreatePartnerRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Settings.Write", cancellationToken);

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Partner name is required.", nameof(request.Name));
        }

        var tenantId = _tenantProvider.GetCurrentTenantId();
        var now = _clock.UtcNow;

        var partner = new Partner
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = request.Name.Trim(),
            Status = string.IsNullOrWhiteSpace(request.Status) ? "Active" : request.Status.Trim(),
            CapabilitiesJson = string.IsNullOrWhiteSpace(request.CapabilitiesJson) ? "[]" : request.CapabilitiesJson.Trim(),
            OperatingHoursJson = string.IsNullOrWhiteSpace(request.OperatingHoursJson) ? "{}" : request.OperatingHoursJson.Trim(),
            CreatedAt = now
        };

        _dbContext.Partners.Add(partner);

        await EnsurePartnerPrefundAccountsAsync(
            tenantId,
            partner.Id,
            partner.Name,
            now,
            cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.LogAsync(
            AuditEventNames.PartnerCreated,
            "Partner",
            partner.Id,
            tenantId,
            actorId: null,
            correlationId: null,
            detailsJson: JsonSerializer.Serialize(new
            {
                partner.Id,
                partner.Name,
                partner.Status
            }, JsonOptions),
            cancellationToken: cancellationToken);

        return new CreatePartnerResponse(
            partner.Id,
            partner.Name,
            partner.Status,
            partner.CreatedAt);
    }

    public async Task<PartnerDetail> UpdatePartnerAsync(
        Guid partnerId,
        UpdatePartnerRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Settings.Write", cancellationToken);

        var tenantId = _tenantProvider.GetCurrentTenantId();

        var partner = await _dbContext.Partners
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == partnerId, cancellationToken);

        if (partner == null)
        {
            throw new InvalidOperationException($"Partner {partnerId} not found.");
        }

        if (request.Name != null)
        {
            var name = request.Name.Trim();
            if (name.Length == 0)
            {
                throw new ArgumentException("Partner name cannot be empty.", nameof(request.Name));
            }

            partner.Name = name;
        }

        if (request.Status != null)
        {
            var status = request.Status.Trim();
            if (status.Length == 0)
            {
                throw new ArgumentException("Partner status cannot be empty.", nameof(request.Status));
            }

            partner.Status = status;
        }

        if (request.CapabilitiesJson != null)
        {
            partner.CapabilitiesJson = string.IsNullOrWhiteSpace(request.CapabilitiesJson)
                ? "[]"
                : request.CapabilitiesJson.Trim();
        }

        if (request.OperatingHoursJson != null)
        {
            partner.OperatingHoursJson = string.IsNullOrWhiteSpace(request.OperatingHoursJson)
                ? "{}"
                : request.OperatingHoursJson.Trim();
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.LogAsync(
            AuditEventNames.PartnerUpdated,
            "Partner",
            partner.Id,
            tenantId,
            actorId: null,
            correlationId: null,
            detailsJson: JsonSerializer.Serialize(new
            {
                partner.Id,
                partner.Name,
                partner.Status
            }, JsonOptions),
            cancellationToken: cancellationToken);

        var updated = await GetPartnerAsync(partnerId, cancellationToken);
        if (updated == null)
        {
            throw new InvalidOperationException($"Partner {partnerId} not found after update.");
        }

        return updated;
    }

    public async Task DeletePartnerAsync(
        Guid partnerId,
        CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Settings.Write", cancellationToken);

        var tenantId = _tenantProvider.GetCurrentTenantId();

        var partner = await _dbContext.Partners
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == partnerId, cancellationToken);

        if (partner == null)
        {
            throw new InvalidOperationException($"Partner {partnerId} not found.");
        }

        var branches = await _dbContext.PartnerBranches
            .Where(branch => branch.TenantId == tenantId && branch.PartnerId == partnerId)
            .ToListAsync(cancellationToken);

        var connectors = await _dbContext.Connectors
            .Where(connector => connector.TenantId == tenantId && connector.PartnerId == partnerId)
            .ToListAsync(cancellationToken);

        var connectorIds = connectors
            .Select(connector => connector.Id)
            .ToList();

        var transmissions = connectorIds.Count == 0
            ? new List<Transmission>()
            : await _dbContext.Transmissions
                .Where(transmission => transmission.TenantId == tenantId && connectorIds.Contains(transmission.ConnectorId))
                .ToListAsync(cancellationToken);

        var routingRules = await _dbContext.RoutingRules
            .Where(rule =>
                rule.TenantId == tenantId &&
                (rule.TargetPartnerId == partnerId ||
                 (rule.TargetConnectorId.HasValue && connectorIds.Contains(rule.TargetConnectorId.Value))))
            .ToListAsync(cancellationToken);

        var linkedBillers = await _dbContext.CatalogBillers
            .Where(biller => biller.TenantId == tenantId && biller.CorrespondentPartnerId == partnerId)
            .ToListAsync(cancellationToken);

        if (linkedBillers.Count > 0)
        {
            throw new InvalidOperationException(
                $"Partner {partnerId} cannot be deleted while linked to {linkedBillers.Count} biller(s). Reassign billers first.");
        }

        var fundingAccounts = await _dbContext.PartnerFundingAccounts
            .Where(account => account.TenantId == tenantId && account.PartnerId == partnerId)
            .ToListAsync(cancellationToken);

        _dbContext.Transmissions.RemoveRange(transmissions);
        _dbContext.RoutingRules.RemoveRange(routingRules);
        _dbContext.Connectors.RemoveRange(connectors);
        _dbContext.PartnerBranches.RemoveRange(branches);
        _dbContext.PartnerFundingAccounts.RemoveRange(fundingAccounts);
        _dbContext.Partners.Remove(partner);

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.LogAsync(
            AuditEventNames.PartnerDeleted,
            "Partner",
            partnerId,
            tenantId,
            actorId: null,
            correlationId: null,
            detailsJson: JsonSerializer.Serialize(new
            {
                PartnerId = partnerId,
                BranchCount = branches.Count,
                ConnectorCount = connectors.Count,
                RoutingRuleCount = routingRules.Count,
                TransmissionCount = transmissions.Count,
                FundingAccountCount = fundingAccounts.Count
            }, JsonOptions),
            cancellationToken: cancellationToken);
    }

    private async Task EnsurePartnerPrefundAccountsAsync(
        Guid tenantId,
        Guid partnerId,
        string partnerName,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var ledgerId = await _dbContext.Ledgers
            .AsNoTracking()
            .Where(ledger => ledger.TenantId == tenantId)
            .Select(ledger => (Guid?)ledger.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (!ledgerId.HasValue)
        {
            throw new InvalidOperationException($"Tenant {tenantId} does not have a ledger.");
        }

        var currencyCodes = await GetPartnerPrefundCurrencyCodesAsync(tenantId, cancellationToken);

        var existingFundingAccounts = await _dbContext.PartnerFundingAccounts
            .Where(account => account.TenantId == tenantId && account.PartnerId == partnerId && account.AccountRole == PrefundAccountRole)
            .ToListAsync(cancellationToken);

        foreach (var currencyCode in currencyCodes)
        {
            var existingFundingAccount = existingFundingAccounts
                .FirstOrDefault(account => account.Currency == currencyCode);

            if (existingFundingAccount != null)
            {
                continue;
            }

            var accountCode = BuildPartnerPrefundAccountCode(partnerId, currencyCode);

            var ledgerAccount = await _dbContext.LedgerAccounts
                .FirstOrDefaultAsync(account => account.TenantId == tenantId && account.Code == accountCode, cancellationToken);

            if (ledgerAccount == null)
            {
                ledgerAccount = new LedgerAccount
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    LedgerId = ledgerId.Value,
                    AccountType = "Asset",
                    Name = $"Due From Partner {partnerName} ({currencyCode})",
                    Code = accountCode,
                    DimensionsJson = JsonSerializer.Serialize(new
                    {
                        partnerId,
                        currency = currencyCode,
                        accountRole = PrefundAccountRole
                    }),
                    CreatedAt = now
                };

                _dbContext.LedgerAccounts.Add(ledgerAccount);
            }

            _dbContext.PartnerFundingAccounts.Add(new PartnerFundingAccount
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                PartnerId = partnerId,
                LedgerAccountId = ledgerAccount.Id,
                Currency = currencyCode,
                AccountRole = PrefundAccountRole,
                Status = "Active",
                CreatedAt = now
            });
        }
    }

    private async Task<List<string>> GetPartnerPrefundCurrencyCodesAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var tenantCurrencies = await _dbContext.TenantCurrencies
            .AsNoTracking()
            .Where(tenantCurrency => tenantCurrency.TenantId == tenantId)
            .Join(
                _dbContext.Currencies.AsNoTracking(),
                tenantCurrency => tenantCurrency.CurrencyId,
                currency => currency.Id,
                (_, currency) => currency.Code)
            .Distinct()
            .OrderBy(code => code)
            .ToListAsync(cancellationToken);

        if (tenantCurrencies.Count > 0)
        {
            return tenantCurrencies;
        }

        var defaultCurrency = await _dbContext.Tenants
            .AsNoTracking()
            .Where(tenant => tenant.Id == tenantId)
            .Select(tenant => tenant.DefaultCurrency)
            .FirstOrDefaultAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(defaultCurrency))
        {
            return new List<string> { defaultCurrency.Trim().ToUpperInvariant() };
        }

        return new List<string> { "USD" };
    }

    private static string BuildPartnerPrefundAccountCode(Guid partnerId, string currencyCode)
    {
        var partnerCode = partnerId.ToString("N")[..12].ToUpperInvariant();
        return $"1300-{partnerCode}-{currencyCode}";
    }
}
