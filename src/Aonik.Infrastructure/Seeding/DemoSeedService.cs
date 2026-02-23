using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Application.Abstractions.Observability;
using Aonik.Application.Abstractions.Persistence;
using Aonik.Platform.Contracts.Models.Seeding;
using Aonik.Application.Services.Compliance;
using Aonik.Platform.Contracts.Services.Compliance;
using Aonik.Platform.Contracts.Services.Identity;
using Aonik.Platform.Contracts.Services.Seeding;
using Aonik.Infrastructure.Persistence.Seed;
using Aonik.SharedKernel.Abstractions;
using Aonik.Domain.Catalog.Entities;
using Aonik.Domain.Pricing.Entities;
using Aonik.Platform.Entities.Party;
using Aonik.Platform.Entities.Settings;
using Aonik.Platform.Entities.Identity;
using Aonik.Domain.Ledger.Entities;
using Aonik.Domain.Partners.Entities;
using Aonik.Domain.PersonalFinance.Entities;
using Aonik.Application.Models.Catalog;
using Aonik.Application.Models.Pricing;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Aonik.Infrastructure.Seeding;

public class DemoSeedService : IDemoSeedService
{
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> TenantSeedLocks = new();

    private const string PersonPartyType = "Person";
    private const string BusinessPartyType = "Business";
    private const string PrefundAccountRole = "PrefundAsset";
    private const string PartnerPrefundSeedSourceType = "PartnerPrefundSeed";

    private const string DemoSeedKey = "DemoSeed.BillPayment";
    private const string CrossBorderDemoSeedKey = "DemoSeed.CrossBorderPayments";
    private readonly IAonikDbContext _dbContext;
    private readonly IClock _clock;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IAuditLogWriter _auditLogWriter;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ICorrelationContext _correlationContext;
    private readonly IPermissionService _permissionService;
    private readonly ITenantContext _tenantContext;

    private static readonly Guid UtilitiesCategoryId = Guid.Parse("9de53a10-0f7c-4ce5-9ef4-6305656135e1");
    private static readonly Guid EcgBillerId = Guid.Parse("aa7d7c1c-4aab-4b51-8b0a-155d42c328f8");
    private static readonly Guid GhanaWaterBillerId = Guid.Parse("0f3b7b2a-c5c2-4d06-b8a2-6f3f28f0b2c5");
    private static readonly Guid EcgPrepaidServiceId = Guid.Parse("3c1f6a6a-73cf-4be0-a15d-2ed45e8d3577");
    private static readonly Guid GhanaWaterServiceId = Guid.Parse("c4a7f65d-2f7a-4b77-9a7c-5c9c9b8a7c91");
    private static readonly Guid EcgPostpaidServiceId = Guid.Parse("9e1a2ff2-7f48-45fd-9af7-3d2d9cf5241e");
    private static readonly Guid GhanaWaterPrepaidServiceId = Guid.Parse("8f80cb7d-fc6e-4b8f-8998-0f683ecf3f58");
    private static readonly Guid DemoPayerPartyId = Guid.Parse("bfe9921e-2f3e-4c56-b8d1-4f5b2a7c3d44");
    private static readonly Guid DemoReceiverPartyId = Guid.Parse("2a3e1f59-44f7-4df4-a8f1-936f9d9d13cd");
    private static readonly Guid DemoRelationshipId = Guid.Parse("c90127f4-9b45-4a8e-9b90-7d0f3d4e65cc");
    private static readonly Guid DemoFxQuoteId = Guid.Parse("9a8d9f56-b91b-4d1a-8f7a-2e12a54e50e2");
    private static readonly Guid DemoFeePolicyId = Guid.Parse("7b6b3b5d-91b9-4d25-8f2c-ead45812c1a1");
    private static readonly Guid DemoLimitsPolicyId = Guid.Parse("5a8dd1d8-1f47-41f5-9e8d-1ef1e7c7880a");
    private static readonly Guid NigeriaUtilitiesCategoryId = Guid.Parse("6d67a8f3-9242-4d42-a7fc-a097b2f8f13a");
    private static readonly Guid KenyaUtilitiesCategoryId = Guid.Parse("9f2287f1-6c0e-4c53-bf67-a6fcbbdd4194");
    private static readonly Guid SouthAfricaUtilitiesCategoryId = Guid.Parse("dc9fd7e0-f74f-4181-b643-fdf678c113f6");
    private static readonly Guid IkejaElectricBillerId = Guid.Parse("eec98e01-8ab4-4f61-a4d4-c4409f1f596e");
    private static readonly Guid LagosWaterBillerId = Guid.Parse("d59fb89a-efcf-4069-a4f8-7ed1cf1b9fd9");
    private static readonly Guid KenyaPowerBillerId = Guid.Parse("f5f91117-a466-4f89-b7e4-ce1b6ace9f9a");
    private static readonly Guid CityPowerBillerId = Guid.Parse("3d6622ff-7661-4f43-bf6a-2a3f6ae97f8c");
    private static readonly Guid IkejaPrepaidServiceId = Guid.Parse("60d7de6b-e579-412a-bbe6-f7fc6cad2b2d");
    private static readonly Guid IkejaPostpaidServiceId = Guid.Parse("a7d13065-8e2a-47c9-a84f-4f9725448e2b");
    private static readonly Guid LagosWaterServiceId = Guid.Parse("bc767227-e727-4370-b54b-a52cd57774e8");
    private static readonly Guid LagosWaterPrepaidServiceId = Guid.Parse("f6bbca26-05b4-47c3-afca-f3f7453c189f");
    private static readonly Guid KenyaPowerServiceId = Guid.Parse("61a14f31-37e8-4fc1-8f8a-22ca7ddd8efe");
    private static readonly Guid KenyaPowerPostpaidServiceId = Guid.Parse("46ddbdce-446e-4898-8a4b-b8a28f6999aa");
    private static readonly Guid CityPowerServiceId = Guid.Parse("5b997ce8-66dc-4fc2-9e1b-a7144a3294b6");
    private static readonly Guid CityPowerPostpaidServiceId = Guid.Parse("6c99f4f0-8d6b-4e5d-a6a5-65bdcb7e6f4f");
    private static readonly Guid NigeriaPartnerId = Guid.Parse("f8b8a6cb-7f85-45aa-84af-7ce4d17172af");
    private static readonly Guid GhanaPartnerId = Guid.Parse("5f8fa8a8-f16a-4256-b7ea-32a8322c2f8d");
    private static readonly Guid KenyaPartnerId = Guid.Parse("3da50f8d-5f9b-4c27-96f1-c7c603ec073d");
    private static readonly Guid SouthAfricaPartnerId = Guid.Parse("fca95d87-cf29-4e57-b931-f26f76f052da");
    private static readonly Guid NigeriaBranchId = Guid.Parse("9021f646-0525-43b8-bfb7-6fd7482c5f95");
    private static readonly Guid GhanaBranchId = Guid.Parse("4f26abca-97a9-4806-8274-243cc87ecf9a");
    private static readonly Guid KenyaBranchId = Guid.Parse("ce53f9dd-8d80-4f49-acaf-9fac89efb4ba");
    private static readonly Guid SouthAfricaBranchId = Guid.Parse("ecf19036-575c-4226-ae93-50e7f6708f18");
    private static readonly Guid NigeriaConnectorId = Guid.Parse("6dbd8515-d115-42e3-a3b9-721e4f0ad08a");
    private static readonly Guid GhanaConnectorId = Guid.Parse("f4aa2e03-03b7-4af7-850d-56919d2f5c86");
    private static readonly Guid KenyaConnectorId = Guid.Parse("ab58f337-5f04-4e5e-bec8-7d713267464f");
    private static readonly Guid SouthAfricaConnectorId = Guid.Parse("ac085fd8-6afa-49ef-a2f1-c4915334ad1d");
    private static readonly Guid NigeriaRoutingRuleId = Guid.Parse("890d6a45-5558-46c7-bf6b-8df3a15ce7f9");
    private static readonly Guid GhanaRoutingRuleId = Guid.Parse("e771f089-3167-42c5-98cd-f85e947e5ddf");
    private static readonly Guid KenyaRoutingRuleId = Guid.Parse("0072252c-8f31-485e-a0ac-8ff8df5263d9");
    private static readonly Guid SouthAfricaRoutingRuleId = Guid.Parse("6e93d3eb-c8f6-4c5d-abd4-c7759a5048ab");
    private static readonly Guid TundePartyId = Guid.Parse("5ef5e008-8d3d-485f-8718-67ab4d4da2cf");
    private static readonly Guid AdwoaPartyId = Guid.Parse("5c882622-4958-4e0e-8cad-cb20f6e720ca");
    private static readonly Guid PeterPartyId = Guid.Parse("cb94f5cd-ed2d-4e95-99be-6d8bb6acdbbe");
    private static readonly Guid NalediPartyId = Guid.Parse("40ee8396-c640-4d0a-a262-2d32743cb95a");
    private static readonly Guid AishaPartyId = Guid.Parse("da32f3f2-07fa-41af-9792-6a4a0b8f5074");
    private static readonly Guid KofiPartyId = Guid.Parse("563b6348-c34f-423f-8b22-c92ca6f9f195");
    private static readonly Guid AcmeImportsPartyId = Guid.Parse("f0f72256-f43b-455a-af08-8fab70115794");
    private static readonly Guid SafariFreightPartyId = Guid.Parse("087f4f38-a018-4b65-a47e-2e287d74f8f5");
    private static readonly Guid OliviaPartyId = Guid.Parse("fb229001-e24c-4fd3-a87d-e0458a2cf8cb");
    private static readonly Guid LiamPartyId = Guid.Parse("3f48a4fc-c7ce-4f78-af09-a2796e735f85");
    private static readonly Guid TundeAdwoaRelationshipId = Guid.Parse("0d9cb5b0-9d5f-41a8-9f6f-e6ae45e4dd9f");
    private static readonly Guid TundePeterRelationshipId = Guid.Parse("15f65e53-3252-4a82-b6b9-f97b8b9d7199");
    private static readonly Guid NalediAishaRelationshipId = Guid.Parse("2f29a6f4-af26-4c2a-a6b1-0d64874fd6b3");
    private static readonly Guid KofiAmaRelationshipId = Guid.Parse("93c83fed-d56a-4ca6-8f44-4512f50eeecb");
    private static readonly Guid OliviaNalediRelationshipId = Guid.Parse("f28be4e6-e5bc-43a5-8c52-cf3906f6c16f");
    private static readonly Guid LiamKwameRelationshipId = Guid.Parse("0fd357dd-58a3-481b-a36d-5e7efde0ebca");
    private static readonly Guid FamilyHouseholdId = Guid.Parse("96f58c5f-82f3-41b8-beb6-bf11fbcce5c2");
    private static readonly Guid ProfessionalsHouseholdId = Guid.Parse("89b29ec1-a771-4926-8897-ec7408ee8917");
    private static readonly Guid FamilyHouseholdMemberId = Guid.Parse("09f17349-c107-46b7-a3c9-c2bc42053a7e");
    private static readonly Guid ProfessionalsHouseholdMemberId = Guid.Parse("a8fdb3f6-8f7f-40f4-b55d-07d1997aebc7");
    private static readonly Guid NgnKesFxQuoteId = Guid.Parse("32cc8c2b-76eb-4f97-b715-3bc8474f4ec7");
    private static readonly Guid NgnZarFxQuoteId = Guid.Parse("f4366a9d-550e-4cb2-af36-4134e6f62050");
    private static readonly Guid UsdGhsFxQuoteId = Guid.Parse("6d81b4d5-e8e0-46c0-ae17-a43eca0bfe61");
    private static readonly Guid UsdKesFxQuoteId = Guid.Parse("a30b90e1-784c-4fb9-ab2b-3941a93bc981");
    private static readonly Guid UsdZarFxQuoteId = Guid.Parse("a244a0b1-b6d4-4f95-827a-7001243b9d58");
    private static readonly Guid GbpNgnFxQuoteId = Guid.Parse("1496b1ee-6af8-4744-a740-239b4f8b8136");
    private static readonly Guid GbpGhsFxQuoteId = Guid.Parse("ca2992ea-9f4f-4347-98ea-65f8872ef8e4");
    private static readonly Guid GbpKesFxQuoteId = Guid.Parse("4874f7ab-1368-4414-b595-249143ca25da");
    private static readonly Guid GbpZarFxQuoteId = Guid.Parse("5cf25d4d-2834-4027-b84a-500f5f6e113f");
    private static readonly Guid CrossBorderBand1FeePolicyId = Guid.Parse("af7ae2fe-2f1d-4e8a-b6f1-2d3ce43af183");
    private static readonly Guid CrossBorderBand2FeePolicyId = Guid.Parse("6f87556a-8369-425d-a9ff-85082f7c3767");
    private static readonly Guid CrossBorderBand3FeePolicyId = Guid.Parse("45eb59de-9970-476f-8ee5-cc2f196f998e");
    private static readonly Guid CrossBorderKesFeePolicyId = Guid.Parse("8eebac13-e53c-4d2a-b00f-825026f0f3fb");
    private static readonly Guid CrossBorderZarFeePolicyId = Guid.Parse("06f9246e-ff04-4d5c-86f7-11ba24a57cc8");
    private static readonly Guid KenyaLimitsPolicyId = Guid.Parse("089177ce-b8ef-4f1a-8b95-ebcd6b6892e6");
    private static readonly Guid SouthAfricaLimitsPolicyId = Guid.Parse("bade6de0-6272-4e5d-9b3b-7f6f42fec4c3");

    public DemoSeedService(
        IAonikDbContext dbContext,
        IClock clock,
        ILoggerFactory loggerFactory,
        IAuditLogWriter auditLogWriter,
        ICurrentUserProvider currentUserProvider,
        ICorrelationContext correlationContext,
        IPermissionService permissionService,
        ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _clock = clock;
        _loggerFactory = loggerFactory;
        _auditLogWriter = auditLogWriter;
        _currentUserProvider = currentUserProvider;
        _correlationContext = correlationContext;
        _permissionService = permissionService;
        _tenantContext = tenantContext;
    }

    public async Task<DemoSeedResult> SeedAsync(Guid tenantId, string? seedType = null, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Tenants.Write", cancellationToken);

        var normalizedSeedType = NormalizeSeedType(seedType);

        var tenantExists = await _dbContext.Tenants.AnyAsync(t => t.Id == tenantId, cancellationToken);
        if (!tenantExists)
        {
            throw new InvalidOperationException($"Tenant {tenantId} not found");
        }

        var tenantSeedLock = TenantSeedLocks.GetOrAdd(tenantId, _ => new SemaphoreSlim(1, 1));
        await tenantSeedLock.WaitAsync(cancellationToken);

        try
        {
            _tenantContext.TenantId = tenantId;
            _tenantContext.ResolutionSource = "AdminTenantAction";

            var operations = new List<string>();

            var identitySeed = new IdentitySeedService((IAonikDbContext)_dbContext, _loggerFactory.CreateLogger<IdentitySeedService>());
            await identitySeed.SeedAsync(cancellationToken);
            operations.Add("IdentitySeed");
            ClearTrackingIfSupported(_dbContext);

            var catalogSeed = new CatalogSeedService((IAonikDbContext)_dbContext, _loggerFactory.CreateLogger<CatalogSeedService>());
            await catalogSeed.SeedAsync(cancellationToken);
            operations.Add("CatalogSeed");
            ClearTrackingIfSupported(_dbContext);

            await EnsureTenantAdminRoleAsync(tenantId, operations, cancellationToken);
            ClearTrackingIfSupported(_dbContext);
            var billCollectionPartnerId = await EnsureBillCollectionPartnerAsync(tenantId, operations, cancellationToken);
            ClearTrackingIfSupported(_dbContext);
            var catalogIds = await SeedCatalogAsync(tenantId, billCollectionPartnerId, operations, cancellationToken);
            ClearTrackingIfSupported(_dbContext);
            var partyIds = await SeedPartiesAsync(tenantId, operations, cancellationToken);
            ClearTrackingIfSupported(_dbContext);
            var pricingIds = await SeedPricingAsync(tenantId, operations, cancellationToken);
            ClearTrackingIfSupported(_dbContext);
            await UpsertMarkerAsync(tenantId, catalogIds, partyIds, pricingIds, operations, cancellationToken);
            ClearTrackingIfSupported(_dbContext);

            if (normalizedSeedType == DemoSeedTypes.CrossBorderPayments)
            {
                await EnsureUkHomeBaseAsync(tenantId, operations, cancellationToken);
                ClearTrackingIfSupported(_dbContext);
                var tenantCoverage = await SeedCrossBorderTenantCoverageAsync(tenantId, operations, cancellationToken);
                ClearTrackingIfSupported(_dbContext);
                var partnerNetwork = await SeedCrossBorderPartnerNetworkAsync(tenantId, operations, cancellationToken);
                ClearTrackingIfSupported(_dbContext);
                var crossBorderCatalog = await SeedCrossBorderCatalogAsync(tenantId, partnerNetwork, operations, cancellationToken);
                ClearTrackingIfSupported(_dbContext);
                var crossBorderParties = await SeedCrossBorderPartiesAsync(tenantId, operations, cancellationToken);
                ClearTrackingIfSupported(_dbContext);
                var householdIds = await SeedHouseholdsAsync(tenantId, operations, cancellationToken);
                ClearTrackingIfSupported(_dbContext);
                var crossBorderPricing = await SeedCrossBorderPricingAsync(tenantId, operations, cancellationToken);
                ClearTrackingIfSupported(_dbContext);

                await UpsertCrossBorderMarkerAsync(
                    tenantId,
                    normalizedSeedType,
                    catalogIds,
                    partyIds,
                    pricingIds,
                    tenantCoverage,
                    partnerNetwork,
                    crossBorderCatalog,
                    crossBorderParties,
                    householdIds,
                    crossBorderPricing,
                    operations,
                    cancellationToken);
            }

            var now = _clock.UtcNow;
            var userId = _currentUserProvider.GetCurrentUserId();

            await _auditLogWriter.LogAsync(
                AuditEventNames.TenantDemoSeeded,
                "TenantDemoSeed",
                tenantId,
                tenantId,
                userId,
                _correlationContext.CorrelationId,
                JsonSerializer.Serialize(new { tenantId, seedType = normalizedSeedType, operations }),
                cancellationToken);

            return new DemoSeedResult(tenantId, normalizedSeedType, now, operations);
        }
        finally
        {
            tenantSeedLock.Release();
        }
    }

    private async Task EnsureTenantAdminRoleAsync(Guid tenantId, List<string> operations, CancellationToken cancellationToken)
    {
        var userId = _currentUserProvider.GetCurrentUserId();
        if (!userId.HasValue)
        {
            return;
        }

        var tenantAdminRole = await _dbContext.Roles
            .FirstOrDefaultAsync(role => role.TenantId == tenantId && role.Name == "TenantAdmin", cancellationToken);

        if (tenantAdminRole == null)
        {
            tenantAdminRole = new Role
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "TenantAdmin",
                CreatedAt = _clock.UtcNow,
                CreatedBy = userId
            };
            _dbContext.Roles.Add(tenantAdminRole);
            await _dbContext.SaveChangesAsync(cancellationToken);
            operations.Add("Created TenantAdmin role");
        }

        var hasRole = await _dbContext.UserRoles
            .AnyAsync(link => link.UserId == userId && link.RoleId == tenantAdminRole.Id, cancellationToken);

        if (!hasRole)
        {
            _dbContext.UserRoles.Add(new UserRole
            {
                UserId = userId.Value,
                RoleId = tenantAdminRole.Id,
                CreatedAt = _clock.UtcNow,
                CreatedBy = userId
            });

            await _dbContext.SaveChangesAsync(cancellationToken);
            operations.Add("Assigned TenantAdmin role");
        }
    }

    private async Task<Guid> EnsureBillCollectionPartnerAsync(
        Guid tenantId,
        List<string> operations,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var userId = _currentUserProvider.GetCurrentUserId();
        const string partnerName = "Gold Coast Bill Hub";

        var partner = await _dbContext.Partners
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Name == partnerName, cancellationToken);

        var capabilitiesJson = JsonSerializer.Serialize(new[] { "BILLPAY", "COLLECTIONS" });
        var operatingHoursJson = JsonSerializer.Serialize(new
        {
            timezone = "Africa/Accra",
            weekdays = "06:00-22:00",
            weekends = "08:00-20:00"
        });

        if (partner == null)
        {
            partner = new Partner
            {
                Id = GhanaPartnerId,
                TenantId = tenantId,
                Name = partnerName,
                Status = "Active",
                CapabilitiesJson = capabilitiesJson,
                OperatingHoursJson = operatingHoursJson,
                CreatedAt = now,
                CreatedBy = userId
            };

            _dbContext.Partners.Add(partner);
        }
        else
        {
            partner.Status = "Active";
            partner.CapabilitiesJson = capabilitiesJson;
            partner.OperatingHoursJson = operatingHoursJson;
            partner.UpdatedAt = now;
            partner.UpdatedBy = userId;
        }

        await EnsurePartnerPrefundAccountAsync(
            tenantId,
            partner.Id,
            partner.Name,
            "GHS",
            90000m,
            now,
            userId,
            cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        operations.Add("Ensured BillCollection GH partner and prefund account");
        return partner.Id;
    }

    private async Task EnsurePartnerPrefundAccountAsync(
        Guid tenantId,
        Guid partnerId,
        string partnerName,
        string currencyCode,
        decimal openingBalance,
        DateTime now,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        var normalizedCurrency = currencyCode.Trim().ToUpperInvariant();
        var ledgerId = await GetTenantLedgerIdAsync(tenantId, cancellationToken);

        var fundingAccount = await _dbContext.PartnerFundingAccounts
            .FirstOrDefaultAsync(account =>
                account.TenantId == tenantId &&
                account.PartnerId == partnerId &&
                account.Currency == normalizedCurrency &&
                account.AccountRole == PrefundAccountRole,
                cancellationToken);

        var accountCode = BuildPartnerPrefundAccountCode(partnerId, normalizedCurrency);
        var ledgerAccount = await _dbContext.LedgerAccounts
            .FirstOrDefaultAsync(account => account.TenantId == tenantId && account.Code == accountCode, cancellationToken);

        if (ledgerAccount == null)
        {
            ledgerAccount = new LedgerAccount
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                LedgerId = ledgerId,
                AccountType = "Asset",
                Name = $"Due From Partner {partnerName} ({normalizedCurrency})",
                Code = accountCode,
                DimensionsJson = JsonSerializer.Serialize(new
                {
                    partnerId,
                    currency = normalizedCurrency,
                    accountRole = PrefundAccountRole
                }),
                CreatedAt = now,
                CreatedBy = userId
            };

            _dbContext.LedgerAccounts.Add(ledgerAccount);
        }

        if (fundingAccount == null)
        {
            fundingAccount = new PartnerFundingAccount
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                PartnerId = partnerId,
                LedgerAccountId = ledgerAccount.Id,
                Currency = normalizedCurrency,
                AccountRole = PrefundAccountRole,
                Status = "Active",
                CreatedAt = now,
                CreatedBy = userId
            };

            _dbContext.PartnerFundingAccounts.Add(fundingAccount);
        }
        else
        {
            fundingAccount.LedgerAccountId = ledgerAccount.Id;
            fundingAccount.Currency = normalizedCurrency;
            fundingAccount.Status = "Active";
            fundingAccount.UpdatedAt = now;
            fundingAccount.UpdatedBy = userId;
        }

        await EnsurePartnerPrefundOpeningEntryAsync(
            tenantId,
            ledgerId,
            fundingAccount,
            openingBalance,
            now,
            userId,
            cancellationToken);
    }

    private async Task EnsurePartnerPrefundOpeningEntryAsync(
        Guid tenantId,
        Guid ledgerId,
        PartnerFundingAccount fundingAccount,
        decimal openingBalance,
        DateTime now,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        if (openingBalance <= 0)
        {
            return;
        }

        var hasSeedEntry = await _dbContext.JournalEntries
            .AsNoTracking()
            .AnyAsync(entry =>
                entry.TenantId == tenantId &&
                entry.SourceType == PartnerPrefundSeedSourceType &&
                entry.SourceId == fundingAccount.Id,
                cancellationToken);

        if (hasSeedEntry)
        {
            return;
        }

        var cashAccountId = await ResolveCashLedgerAccountIdAsync(tenantId, cancellationToken);
        var entry = new JournalEntry
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            LedgerId = ledgerId,
            Timestamp = now,
            SourceType = PartnerPrefundSeedSourceType,
            SourceId = fundingAccount.Id,
            Status = "Posted",
            CreatedAt = now,
            CreatedBy = userId,
            Lines = new List<JournalEntryLine>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    LedgerAccountId = fundingAccount.LedgerAccountId,
                    Direction = "Debit",
                    Amount = openingBalance,
                    Currency = fundingAccount.Currency,
                    Narration = "Seed prefund opening balance",
                    DimensionsJson = JsonSerializer.Serialize(new
                    {
                        partnerId = fundingAccount.PartnerId,
                        fundingAccountId = fundingAccount.Id,
                        accountRole = fundingAccount.AccountRole
                    }),
                    CreatedAt = now,
                    CreatedBy = userId
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    LedgerAccountId = cashAccountId,
                    Direction = "Credit",
                    Amount = openingBalance,
                    Currency = fundingAccount.Currency,
                    Narration = "Seed prefund opening balance",
                    DimensionsJson = JsonSerializer.Serialize(new
                    {
                        partnerId = fundingAccount.PartnerId,
                        fundingAccountId = fundingAccount.Id,
                        accountRole = fundingAccount.AccountRole
                    }),
                    CreatedAt = now,
                    CreatedBy = userId
                }
            }
        };

        _dbContext.JournalEntries.Add(entry);
    }

    private async Task<Guid> ResolveCashLedgerAccountIdAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var cashAccountId = await _dbContext.LedgerAccounts
            .AsNoTracking()
            .Where(account => account.TenantId == tenantId && account.Code == "1000")
            .Select(account => (Guid?)account.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (cashAccountId.HasValue)
        {
            return cashAccountId.Value;
        }

        cashAccountId = await _dbContext.LedgerAccounts
            .AsNoTracking()
            .Where(account => account.TenantId == tenantId && account.Name == "Cash")
            .Select(account => (Guid?)account.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (!cashAccountId.HasValue)
        {
            throw new InvalidOperationException($"Tenant {tenantId} does not have a cash ledger account for prefund seeding.");
        }

        return cashAccountId.Value;
    }

    private async Task<Guid> GetTenantLedgerIdAsync(Guid tenantId, CancellationToken cancellationToken)
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

        return ledgerId.Value;
    }

    private static string BuildPartnerPrefundAccountCode(Guid partnerId, string currencyCode)
    {
        var partnerCode = partnerId.ToString("N")[..12].ToUpperInvariant();
        return $"1300-{partnerCode}-{currencyCode}";
    }

    private async Task<(Guid UtilitiesCategoryId, Guid EcgBillerId, Guid WaterBillerId, Guid EcgServiceId, Guid WaterServiceId)> SeedCatalogAsync(
        Guid tenantId,
        Guid billCollectionPartnerId,
        List<string> operations,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var userId = _currentUserProvider.GetCurrentUserId();

        var category = await _dbContext.CatalogBillerCategories
            .FirstOrDefaultAsync(item => item.TenantId == tenantId
                                         && item.CountryCode == "GH"
                                         && item.Name == "Utilities",
                cancellationToken);

        if (category == null)
        {
            category = new CatalogBillerCategory
            {
                Id = UtilitiesCategoryId,
                TenantId = tenantId,
                CountryCode = "GH",
                Name = "Utilities",
                Description = "Electricity and water billers",
                SortOrder = 1,
                IsActive = true,
                CreatedAt = now,
                CreatedBy = userId
            };
            _dbContext.CatalogBillerCategories.Add(category);
            operations.Add("Catalog category seeded");
        }
        else
        {
            category.Description = "Electricity and water billers";
            category.SortOrder = 1;
            category.IsActive = true;
            category.UpdatedAt = now;
            category.UpdatedBy = userId;
        }

        var categoryId = category.Id;
        var ecgBillerId = await UpsertBillerAsync(
            tenantId,
            categoryId,
            EcgBillerId,
            "ECG Power",
            "Ghana's electricity provider.",
            now,
            userId,
            operations,
            cancellationToken,
            billCollectionPartnerId);

        var waterBillerId = await UpsertBillerAsync(
            tenantId,
            categoryId,
            GhanaWaterBillerId,
            "Ghana Water",
            "National water utility.",
            now,
            userId,
            operations,
            cancellationToken,
            billCollectionPartnerId);

        var ecgServiceId = await UpsertServiceAsync(
            tenantId,
            ecgBillerId,
            EcgPrepaidServiceId,
            "BILLPAY.ELECTRICITY.PREPAID",
            "ECG Prepaid Electricity",
            "Prepaid",
            "GHS",
            5,
            500,
            true,
            true,
            BuildServiceFieldsJson(new[]
            {
                new CatalogServiceField("meterNumber", "Meter number", "text", true, 6, 16, null, "Enter meter number", null),
                new CatalogServiceField("customerName", "Customer name", "text", true, 2, 80, null, "Enter customer name", null)
            }),
            JsonSerializer.Serialize(new CatalogServiceValidation(
                $"/catalog/billers/{EcgBillerId}/services/{EcgPrepaidServiceId}/validate",
                "precheck")),
            operations,
            cancellationToken);

        var waterServiceId = await UpsertServiceAsync(
            tenantId,
            waterBillerId,
            GhanaWaterServiceId,
            "BILLPAY.WATER.POSTPAID",
            "Ghana Water Postpaid",
            "Postpaid",
            "GHS",
            10,
            1000,
            false,
            false,
            BuildServiceFieldsJson(new[]
            {
                new CatalogServiceField("accountNumber", "Account number", "text", true, 6, 20, null, "Enter account number", null)
            }),
            null,
            operations,
            cancellationToken);

        await UpsertServiceAsync(
            tenantId,
            ecgBillerId,
            EcgPostpaidServiceId,
            "BILLPAY.ELECTRICITY.POSTPAID.GH.ECG",
            "ECG Postpaid Electricity",
            "Postpaid",
            "GHS",
            20,
            2000,
            false,
            true,
            BuildServiceFieldsJson(new[]
            {
                new CatalogServiceField("accountNumber", "Account number", "text", true, 8, 20, null, "Enter account number", null),
                new CatalogServiceField("customerName", "Customer name", "text", true, 2, 80, null, "Enter customer name", null)
            }),
            JsonSerializer.Serialize(new CatalogServiceValidation(
                $"/catalog/billers/{EcgBillerId}/services/{EcgPostpaidServiceId}/validate",
                "precheck")),
            operations,
            cancellationToken);

        await UpsertServiceAsync(
            tenantId,
            waterBillerId,
            GhanaWaterPrepaidServiceId,
            "BILLPAY.WATER.PREPAID.GH.GWL",
            "Ghana Water Prepaid",
            "Prepaid",
            "GHS",
            5,
            1000,
            true,
            true,
            BuildServiceFieldsJson(new[]
            {
                new CatalogServiceField("meterNumber", "Meter number", "text", true, 6, 16, null, "Enter meter number", null),
                new CatalogServiceField("customerName", "Customer name", "text", true, 2, 80, null, "Enter customer name", null)
            }),
            JsonSerializer.Serialize(new CatalogServiceValidation(
                $"/catalog/billers/{GhanaWaterBillerId}/services/{GhanaWaterPrepaidServiceId}/validate",
                "precheck")),
            operations,
            cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return (categoryId, ecgBillerId, waterBillerId, ecgServiceId, waterServiceId);
    }

    private async Task<Guid> UpsertBillerAsync(
        Guid tenantId,
        Guid categoryId,
        Guid billerId,
        string name,
        string description,
        DateTime now,
        Guid? userId,
        List<string> operations,
        CancellationToken cancellationToken,
        Guid correspondentPartnerId,
        string countryCode = "GH")
    {
        var biller = await _dbContext.CatalogBillers
            .FirstOrDefaultAsync(item => item.TenantId == tenantId
                                         && item.CountryCode == countryCode
                                         && item.Name == name,
                cancellationToken);

        if (biller == null)
        {
            biller = new CatalogBiller
            {
                Id = billerId,
                TenantId = tenantId,
                CategoryId = categoryId,
                CountryCode = countryCode,
                Name = name,
                Description = description,
                CorrespondentPartnerId = correspondentPartnerId,
                SupportEmail = "support@aonik.demo",
                SupportPhone = "+233-000-0000",
                IsActive = true,
                IsFeatured = true,
                SortOrder = 1,
                CreatedAt = now,
                CreatedBy = userId
            };
            _dbContext.CatalogBillers.Add(biller);
            operations.Add($"Catalog biller seeded: {name}");
        }
        else
        {
            biller.CategoryId = categoryId;
            biller.Name = name;
            biller.Description = description;
            biller.CountryCode = countryCode;
            biller.CorrespondentPartnerId = correspondentPartnerId;
            biller.IsActive = true;
            biller.UpdatedAt = now;
            biller.UpdatedBy = userId;
        }

        return biller.Id;
    }

    private async Task<Guid> UpsertServiceAsync(
        Guid tenantId,
        Guid billerId,
        Guid serviceId,
        string serviceCode,
        string name,
        string type,
        string currency,
        decimal minAmount,
        decimal maxAmount,
        bool supportsPartial,
        bool requiresValidation,
        string fieldsJson,
        string? validationJson,
        List<string> operations,
        CancellationToken cancellationToken)
    {
        var service = await _dbContext.CatalogBillerServices
            .FirstOrDefaultAsync(item => item.TenantId == tenantId
                                         && item.ServiceCode == serviceCode,
                cancellationToken);

        var now = _clock.UtcNow;
        var userId = _currentUserProvider.GetCurrentUserId();

        if (service == null)
        {
            service = new CatalogBillerService
            {
                Id = serviceId,
                TenantId = tenantId,
                BillerId = billerId,
                ServiceCode = serviceCode,
                Name = name,
                Type = type,
                Currency = currency,
                MinAmount = minAmount,
                MaxAmount = maxAmount,
                SupportsPartialPayment = supportsPartial,
                RequiresValidation = requiresValidation,
                IsActive = true,
                FieldsJson = fieldsJson,
                ValidationJson = validationJson,
                SortOrder = 1,
                CreatedAt = now,
                CreatedBy = userId
            };
            _dbContext.CatalogBillerServices.Add(service);
            operations.Add($"Catalog service seeded: {name}");
        }
        else
        {
            service.BillerId = billerId;
            service.ServiceCode = serviceCode;
            service.Name = name;
            service.Type = type;
            service.Currency = currency;
            service.MinAmount = minAmount;
            service.MaxAmount = maxAmount;
            service.SupportsPartialPayment = supportsPartial;
            service.RequiresValidation = requiresValidation;
            service.IsActive = true;
            service.FieldsJson = fieldsJson;
            service.ValidationJson = validationJson;
            service.UpdatedAt = now;
            service.UpdatedBy = userId;
        }

        return service.Id;
    }

    private static string BuildServiceFieldsJson(IEnumerable<CatalogServiceField> fields)
    {
        return JsonSerializer.Serialize(fields);
    }

    private async Task<(Guid PayerPartyId, Guid ReceiverPartyId, Guid RelationshipId)> SeedPartiesAsync(
        Guid tenantId,
        List<string> operations,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var userId = _currentUserProvider.GetCurrentUserId();

        var payerEmail = "kwame.mensah@mailinator.com";
        var receiverEmail = "ama.boateng@mailinator.com";

        var payerParty = await _dbContext.Parties
            .Include(party => party.Contacts)
            .FirstOrDefaultAsync(party => party.TenantId == tenantId
                                          && party.Id == DemoPayerPartyId,
                cancellationToken);

        if (payerParty == null)
        {
            payerParty = await _dbContext.Parties
                .Include(party => party.Contacts)
                .FirstOrDefaultAsync(party => party.TenantId == tenantId
                                              && party.Contacts.Any(contact => contact.Type == "Email" && contact.Value == payerEmail),
                    cancellationToken);
        }

        if (payerParty == null)
        {
            payerParty = new Party
            {
                Id = DemoPayerPartyId,
                TenantId = tenantId,
                PartyType = PersonPartyType,
                DisplayName = "Kwame Mensah",
                Status = "Active",
                CustomerTierCode = "Retail",
                CreatedAt = now,
                CreatedBy = userId
            };
            _dbContext.Parties.Add(payerParty);
            operations.Add("Seeded payer party");
        }

        payerParty.PartyType = PersonPartyType;
        payerParty.DisplayName = "Kwame Mensah";
        payerParty.Status = "Active";
        payerParty.CustomerTierCode = "Retail";
        payerParty.UpdatedAt = now;
        payerParty.UpdatedBy = userId;

        await UpsertPartyContactsAsync(payerParty, now, payerEmail, "+234800000000");
        await UpsertPersonProfileAsync(payerParty.Id, "Kwame", "Mensah", "NG", now, userId, cancellationToken);
        await EnsureCustomerRoleAssignmentAsync(tenantId, payerParty.Id, now, userId, cancellationToken);

        var receiverParty = await _dbContext.Parties
            .Include(party => party.Contacts)
            .FirstOrDefaultAsync(party => party.TenantId == tenantId
                                          && party.Id == DemoReceiverPartyId,
                cancellationToken);

        if (receiverParty == null)
        {
            receiverParty = await _dbContext.Parties
                .Include(party => party.Contacts)
                .FirstOrDefaultAsync(party => party.TenantId == tenantId
                                              && party.Contacts.Any(contact => contact.Type == "Email" && contact.Value == receiverEmail),
                    cancellationToken);
        }

        if (receiverParty == null)
        {
            receiverParty = new Party
            {
                Id = DemoReceiverPartyId,
                TenantId = tenantId,
                PartyType = PersonPartyType,
                DisplayName = "Ama Boateng",
                Status = "Active",
                CreatedAt = now,
                CreatedBy = userId
            };
            _dbContext.Parties.Add(receiverParty);
            operations.Add("Seeded receiver party");
        }

        receiverParty.PartyType = PersonPartyType;
        receiverParty.DisplayName = "Ama Boateng";
        receiverParty.Status = "Active";
        receiverParty.CustomerTierCode = "Retail";
        receiverParty.UpdatedAt = now;
        receiverParty.UpdatedBy = userId;

        await UpsertPartyContactsAsync(receiverParty, now, receiverEmail, "+233200000000");
        await UpsertPersonProfileAsync(receiverParty.Id, "Ama", "Boateng", "GH", now, userId, cancellationToken);
        await EnsureCustomerRoleAssignmentAsync(tenantId, receiverParty.Id, now, userId, cancellationToken);

        var relationship = await _dbContext.PartyRelationships
            .FirstOrDefaultAsync(item => item.TenantId == tenantId
                                         && item.FromPartyId == payerParty.Id
                                         && item.ToPartyId == receiverParty.Id
                                         && item.RelationshipTypeCode == "Friend",
                cancellationToken);

        if (relationship == null)
        {
            _dbContext.PartyRelationships.Add(new PartyRelationship
            {
                Id = DemoRelationshipId,
                TenantId = tenantId,
                FromPartyId = payerParty.Id,
                ToPartyId = receiverParty.Id,
                RelationshipTypeCode = "Friend",
                IsActive = true,
                Notes = "Demo relationship",
                CreatedAt = now,
                CreatedBy = userId
            });
            operations.Add("Seeded party relationship");
        }

        var relationshipId = relationship?.Id ?? DemoRelationshipId;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return (payerParty.Id, receiverParty.Id, relationshipId);
    }

    private Task UpsertPartyContactsAsync(Party party, DateTime now, string email = "kwame.mensah@mailinator.com", string phone = "+234800000000")
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var existingEmail = party.Contacts.FirstOrDefault(contact =>
            contact.Type == "Email" &&
            string.Equals(contact.Value, normalizedEmail, StringComparison.OrdinalIgnoreCase));

        if (existingEmail == null)
        {
            existingEmail = party.Contacts.FirstOrDefault(contact => contact.Type == "Email");
            if (existingEmail == null)
            {
                existingEmail = new PartyContact
                {
                    PartyId = party.Id,
                    Type = "Email",
                    Value = normalizedEmail,
                    IsPrimary = true,
                    CreatedAt = now
                };

                party.Contacts.Add(existingEmail);
            }
            else
            {
                existingEmail.Value = normalizedEmail;
                existingEmail.UpdatedAt = now;
            }
        }
        else
        {
            existingEmail.Value = normalizedEmail;
            existingEmail.UpdatedAt = now;
        }

        existingEmail.IsPrimary = true;

        foreach (var otherEmail in party.Contacts.Where(contact => contact.Type == "Email" && !ReferenceEquals(contact, existingEmail)))
        {
            otherEmail.IsPrimary = false;
        }

        var normalizedPhone = phone.Trim();
        var existingPhone = party.Contacts.FirstOrDefault(contact =>
            contact.Type == "Phone" &&
            string.Equals(contact.Value, normalizedPhone, StringComparison.OrdinalIgnoreCase));

        if (existingPhone == null)
        {
            existingPhone = party.Contacts.FirstOrDefault(contact => contact.Type == "Phone");
            if (existingPhone == null)
            {
                party.Contacts.Add(new PartyContact
                {
                    PartyId = party.Id,
                    Type = "Phone",
                    Value = normalizedPhone,
                    IsPrimary = false,
                    CreatedAt = now
                });
            }
            else
            {
                existingPhone.Value = normalizedPhone;
                existingPhone.UpdatedAt = now;
            }
        }
        else
        {
            existingPhone.Value = normalizedPhone;
            existingPhone.UpdatedAt = now;
        }

        return Task.CompletedTask;
    }

    private async Task UpsertPersonProfileAsync(
        Guid partyId,
        string firstName,
        string lastName,
        string countryCode,
        DateTime now,
        Guid? userId,
        CancellationToken cancellationToken,
        string? nationality = null,
        string? occupation = null)
    {
        var profile = await _dbContext.PersonProfiles
            .FirstOrDefaultAsync(item => item.PartyId == partyId, cancellationToken);

        if (profile == null)
        {
            _dbContext.PersonProfiles.Add(new PersonProfile
            {
                PartyId = partyId,
                FirstName = firstName,
                LastName = lastName,
                CountryCode = countryCode,
                Nationality = nationality,
                Occupation = occupation,
                IdvStatus = "Unverified",
                CreatedAt = now,
                CreatedBy = userId
            });
        }
        else
        {
            profile.FirstName = firstName;
            profile.LastName = lastName;
            profile.CountryCode = countryCode;
            profile.Nationality = nationality;
            profile.Occupation = occupation;
            profile.IdvStatus = "Unverified";
            profile.UpdatedAt = now;
            profile.UpdatedBy = userId;
        }
    }

    private async Task<(Guid FxQuoteId, Guid FeePolicyId, Guid LimitsPolicyId)> SeedPricingAsync(
        Guid tenantId,
        List<string> operations,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var userId = _currentUserProvider.GetCurrentUserId();

        var fxQuote = await _dbContext.FxQuotes
            .FirstOrDefaultAsync(item => item.TenantId == tenantId
                                         && item.BaseCurrency == "NGN"
                                         && item.TargetCurrency == "GHS"
                                         && item.Provider == "DemoRate",
                cancellationToken);
        var fxExpiresAt = now.AddHours(24);

        if (fxQuote == null)
        {
            fxQuote = new FxQuote
            {
                Id = DemoFxQuoteId,
                TenantId = tenantId,
                BaseCurrency = "NGN",
                TargetCurrency = "GHS",
                Rate = 0.0075m,
                ExpiresAt = fxExpiresAt,
                Provider = "DemoRate",
                MetadataJson = "{}",
                CreatedAt = now,
                CreatedBy = userId
            };
            _dbContext.FxQuotes.Add(fxQuote);
            operations.Add("Seeded FX quote");
        }
        else
        {
            fxQuote.Rate = 0.0075m;
            fxQuote.ExpiresAt = fxExpiresAt;
            fxQuote.Provider = "DemoRate";
            fxQuote.UpdatedAt = now;
            fxQuote.UpdatedBy = userId;
        }

        var conditions = new FeePolicyConditions(
            "BILLPAY.ELECTRICITY.PREPAID",
            "NG",
            "GH",
            "NGN",
            "GHS",
            "Retail",
            null,
            null,
            25m,
            500m,
            150,
            "DemoRate",
            "AwayFromZero",
            new List<FeeBreakdownDefinition>
            {
                new("SERVICE_FEE", "Service fee", "Fixed"),
                new("FX_MARKUP", "FX markup", "FxMarkup")
            });

        var feePolicy = await _dbContext.FeePolicies
            .FirstOrDefaultAsync(item => item.TenantId == tenantId
                                         && item.Name == "BillPay-NG-GH-Default",
                cancellationToken);

        if (feePolicy == null)
        {
            feePolicy = new FeePolicy
            {
                Id = DemoFeePolicyId,
                TenantId = tenantId,
                Name = "BillPay-NG-GH-Default",
                FixedFee = 50m,
                PercentageFee = 0.015m,
                ConditionsJson = JsonSerializer.Serialize(conditions),
                IsActive = true,
                CreatedAt = now,
                CreatedBy = userId
            };
            _dbContext.FeePolicies.Add(feePolicy);
            operations.Add("Seeded fee policy");
        }
        else
        {
            feePolicy.Name = "BillPay-NG-GH-Default";
            feePolicy.FixedFee = 50m;
            feePolicy.PercentageFee = 0.015m;
            feePolicy.ConditionsJson = JsonSerializer.Serialize(conditions);
            feePolicy.IsActive = true;
            feePolicy.UpdatedAt = now;
            feePolicy.UpdatedBy = userId;
        }

        var limitsPolicy = await _dbContext.LimitsPolicies
            .FirstOrDefaultAsync(item => item.TenantId == tenantId
                                         && item.ScopeType == "Tenant"
                                         && item.ScopeId == tenantId
                                         && item.Currency == "NGN"
                                         && item.Period == "Daily",
                cancellationToken);

        if (limitsPolicy == null)
        {
            limitsPolicy = new LimitsPolicy
            {
                Id = DemoLimitsPolicyId,
                TenantId = tenantId,
                ScopeType = "Tenant",
                ScopeId = tenantId,
                Currency = "NGN",
                MaxAmount = 1000000m,
                Period = "Daily",
                IsActive = true,
                CreatedAt = now,
                CreatedBy = userId
            };
            _dbContext.LimitsPolicies.Add(limitsPolicy);
            operations.Add("Seeded limits policy");
        }
        else
        {
            limitsPolicy.ScopeType = "Tenant";
            limitsPolicy.ScopeId = tenantId;
            limitsPolicy.Currency = "NGN";
            limitsPolicy.MaxAmount = 1000000m;
            limitsPolicy.Period = "Daily";
            limitsPolicy.IsActive = true;
            limitsPolicy.UpdatedAt = now;
            limitsPolicy.UpdatedBy = userId;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return (fxQuote.Id, feePolicy.Id, limitsPolicy.Id);
    }

    private async Task UpsertMarkerAsync(
        Guid tenantId,
        (Guid UtilitiesCategoryId, Guid EcgBillerId, Guid WaterBillerId, Guid EcgServiceId, Guid WaterServiceId) catalogIds,
        (Guid PayerPartyId, Guid ReceiverPartyId, Guid RelationshipId) partyIds,
        (Guid FxQuoteId, Guid FeePolicyId, Guid LimitsPolicyId) pricingIds,
        List<string> operations,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var userId = _currentUserProvider.GetCurrentUserId();
        var payload = new
        {
            TenantId = tenantId,
            catalogIds.UtilitiesCategoryId,
            catalogIds.EcgBillerId,
            catalogIds.WaterBillerId,
            catalogIds.EcgServiceId,
            catalogIds.WaterServiceId,
            partyIds.PayerPartyId,
            partyIds.ReceiverPartyId,
            partyIds.RelationshipId,
            pricingIds.FxQuoteId,
            pricingIds.FeePolicyId,
            pricingIds.LimitsPolicyId
        };
        var value = JsonSerializer.Serialize(payload);

        var setting = await _dbContext.Settings
            .FirstOrDefaultAsync(item => item.Scope == SettingScope.Tenant
                                         && item.TenantId == tenantId
                                         && item.Key == DemoSeedKey,
                cancellationToken);

        if (setting == null)
        {
            setting = new Setting
            {
                Key = DemoSeedKey,
                Value = value,
                Scope = SettingScope.Tenant,
                TenantId = tenantId,
                CreatedAt = now,
                CreatedBy = userId
            };
            _dbContext.Settings.Add(setting);
            operations.Add("Demo seed marker created");
        }
        else
        {
            setting.Value = value;
            setting.UpdatedAt = now;
            setting.UpdatedBy = userId;
            operations.Add("Demo seed marker updated");
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string NormalizeSeedType(string? seedType)
    {
        if (string.IsNullOrWhiteSpace(seedType))
        {
            return DemoSeedTypes.BillCollection;
        }

        if (string.Equals(seedType, DemoSeedTypes.BillCollection, StringComparison.OrdinalIgnoreCase))
        {
            return DemoSeedTypes.BillCollection;
        }

        if (string.Equals(seedType, DemoSeedTypes.CrossBorderPayments, StringComparison.OrdinalIgnoreCase))
        {
            return DemoSeedTypes.CrossBorderPayments;
        }

        throw new InvalidOperationException($"Unsupported demo seed type '{seedType}'.");
    }

    private async Task EnsureUkHomeBaseAsync(
        Guid tenantId,
        List<string> operations,
        CancellationToken cancellationToken)
    {
        var tenant = await _dbContext.Tenants
            .FirstOrDefaultAsync(item => item.Id == tenantId, cancellationToken);

        if (tenant == null)
        {
            throw new InvalidOperationException($"Tenant {tenantId} not found.");
        }

        var now = _clock.UtcNow;
        var userId = _currentUserProvider.GetCurrentUserId();

        tenant.Country = "GB";
        tenant.DefaultCurrency = "GBP";
        tenant.City ??= "London";
        tenant.StateProvince ??= "England";
        tenant.AddressLine1 ??= "25 Finsbury Circus";

        var supportedCountries = ParseSupportedCountries(tenant.SupportedCountriesJson);
        supportedCountries.Add("GB");
        supportedCountries.Add("NG");
        supportedCountries.Add("GH");
        supportedCountries.Add("KE");
        supportedCountries.Add("ZA");

        tenant.SupportedCountriesJson = JsonSerializer.Serialize(supportedCountries.OrderBy(code => code));
        tenant.UpdatedAt = now;
        tenant.UpdatedBy = userId;

        await _dbContext.SaveChangesAsync(cancellationToken);
        operations.Add("Configured tenant home base to UK (GBP) for Africa billing and remittance");
    }

    private static HashSet<string> ParseSupportedCountries(string? supportedCountriesJson)
    {
        if (string.IsNullOrWhiteSpace(supportedCountriesJson))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var items = JsonSerializer.Deserialize<List<string>>(supportedCountriesJson);
            return items == null
                ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(items.Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item.Trim().ToUpperInvariant()), StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static void ClearTrackingIfSupported(IAonikDbContext dbContext)
    {
        if (dbContext is DbContext efDbContext)
        {
            efDbContext.ChangeTracker.Clear();
        }
    }

    private async Task<(IReadOnlyList<Guid> CountryIds, IReadOnlyList<Guid> CurrencyIds)> SeedCrossBorderTenantCoverageAsync(
        Guid tenantId,
        List<string> operations,
        CancellationToken cancellationToken)
    {
        var countryCodes = new[] { "GB", "NG", "GH", "KE", "ZA" };
        var currencyCodes = new[] { "GBP", "NGN", "GHS", "KES", "ZAR", "USD" };

        var countries = await _dbContext.Countries
            .Where(country => countryCodes.Contains(country.IsoAlpha2))
            .Where(country => country.IsActive)
            .ToListAsync(cancellationToken);

        var currencies = await _dbContext.Currencies
            .Where(currency => currencyCodes.Contains(currency.Code))
            .Where(currency => currency.IsActive)
            .ToListAsync(cancellationToken);

        var missingCountries = countryCodes
            .Where(code => countries.All(country => !string.Equals(country.IsoAlpha2, code, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (missingCountries.Count > 0)
        {
            throw new InvalidOperationException($"Missing reference countries: {string.Join(", ", missingCountries)}.");
        }

        var missingCurrencies = currencyCodes
            .Where(code => currencies.All(currency => !string.Equals(currency.Code, code, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (missingCurrencies.Count > 0)
        {
            throw new InvalidOperationException($"Missing reference currencies: {string.Join(", ", missingCurrencies)}.");
        }

        var existingCountryIds = await _dbContext.TenantCountries
            .Where(item => item.TenantId == tenantId)
            .Select(item => item.CountryId)
            .ToListAsync(cancellationToken);
        var existingCurrencyIds = await _dbContext.TenantCurrencies
            .Where(item => item.TenantId == tenantId)
            .Select(item => item.CurrencyId)
            .ToListAsync(cancellationToken);

        var existingCountrySet = existingCountryIds.ToHashSet();
        var existingCurrencySet = existingCurrencyIds.ToHashSet();

        var now = _clock.UtcNow;
        var userId = _currentUserProvider.GetCurrentUserId();

        foreach (var country in countries)
        {
            if (existingCountrySet.Contains(country.Id))
            {
                continue;
            }

            _dbContext.TenantCountries.Add(new TenantCountry
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CountryId = country.Id,
                CreatedAt = now,
                CreatedBy = userId
            });
        }

        foreach (var currency in currencies)
        {
            if (existingCurrencySet.Contains(currency.Id))
            {
                continue;
            }

            _dbContext.TenantCurrencies.Add(new TenantCurrency
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CurrencyId = currency.Id,
                CreatedAt = now,
                CreatedBy = userId
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        operations.Add("Seeded UK-to-Africa tenant countries and currencies");

        return (
            countries.Select(country => country.Id).ToList(),
            currencies.Select(currency => currency.Id).ToList());
    }

    private async Task<Guid> UpsertCategoryAsync(
        Guid tenantId,
        Guid categoryId,
        string countryCode,
        string name,
        string description,
        int sortOrder,
        DateTime now,
        Guid? userId,
        List<string> operations,
        CancellationToken cancellationToken)
    {
        var category = await _dbContext.CatalogBillerCategories
            .FirstOrDefaultAsync(item => item.TenantId == tenantId
                                         && item.CountryCode == countryCode
                                         && item.Name == name,
                cancellationToken);

        if (category == null)
        {
            category = new CatalogBillerCategory
            {
                Id = categoryId,
                TenantId = tenantId,
                CountryCode = countryCode,
                Name = name,
                Description = description,
                SortOrder = sortOrder,
                IsActive = true,
                CreatedAt = now,
                CreatedBy = userId
            };
            _dbContext.CatalogBillerCategories.Add(category);
            operations.Add($"Catalog category seeded: {countryCode} {name}");
        }
        else
        {
            category.Name = name;
            category.Description = description;
            category.SortOrder = sortOrder;
            category.IsActive = true;
            category.UpdatedAt = now;
            category.UpdatedBy = userId;
        }

        return category.Id;
    }

    private sealed record PartnerRouteSeed(
        string CountryCode,
        Guid PartnerId,
        Guid BranchId,
        Guid ConnectorId,
        Guid RoutingRuleId,
        string PartnerName,
        string City,
        string BranchName,
        int Priority,
        string CurrencyCode,
        decimal OpeningPrefundBalance);

    private async Task<(IReadOnlyDictionary<string, Guid> PartnerIdsByCountry, IReadOnlyDictionary<string, Guid> ConnectorIdsByCountry)> SeedCrossBorderPartnerNetworkAsync(
        Guid tenantId,
        List<string> operations,
        CancellationToken cancellationToken)
    {
        var seeds = new List<PartnerRouteSeed>
        {
            new("NG", NigeriaPartnerId, NigeriaBranchId, NigeriaConnectorId, NigeriaRoutingRuleId, "Naija Utility Switch", "Lagos", "Lagos Operations Hub", 10, "NGN", 3500000m),
            new("GH", GhanaPartnerId, GhanaBranchId, GhanaConnectorId, GhanaRoutingRuleId, "Gold Coast Bill Hub", "Accra", "Accra Settlement Hub", 20, "GHS", 90000m),
            new("KE", KenyaPartnerId, KenyaBranchId, KenyaConnectorId, KenyaRoutingRuleId, "EastPay Kenya", "Nairobi", "Nairobi Operations Hub", 30, "KES", 1800000m),
            new("ZA", SouthAfricaPartnerId, SouthAfricaBranchId, SouthAfricaConnectorId, SouthAfricaRoutingRuleId, "Mzansi Bill Connect", "Johannesburg", "Johannesburg Network Hub", 40, "ZAR", 320000m)
        };

        var partnerIdsByCountry = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        var connectorIdsByCountry = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        var now = _clock.UtcNow;
        var userId = _currentUserProvider.GetCurrentUserId();

        foreach (var seed in seeds)
        {
            var partner = await _dbContext.Partners
                .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Name == seed.PartnerName, cancellationToken);

            var capabilitiesJson = JsonSerializer.Serialize(new[] { "BILLPAY", "PAYOUT", "COLLECTIONS" });
            var operatingHoursJson = JsonSerializer.Serialize(new
            {
                timezone = "Africa/Lagos",
                weekdays = "06:00-22:00",
                weekends = "08:00-20:00"
            });

            if (partner == null)
            {
                partner = new Partner
                {
                    Id = seed.PartnerId,
                    TenantId = tenantId,
                    Name = seed.PartnerName,
                    Status = "Active",
                    CapabilitiesJson = capabilitiesJson,
                    OperatingHoursJson = operatingHoursJson,
                    CreatedAt = now,
                    CreatedBy = userId
                };
                _dbContext.Partners.Add(partner);
            }
            else
            {
                partner.Name = seed.PartnerName;
                partner.Status = "Active";
                partner.CapabilitiesJson = capabilitiesJson;
                partner.OperatingHoursJson = operatingHoursJson;
                partner.UpdatedAt = now;
                partner.UpdatedBy = userId;
            }

            var partnerId = partner.Id;

            await EnsurePartnerPrefundAccountAsync(
                tenantId,
                partnerId,
                seed.PartnerName,
                seed.CurrencyCode,
                seed.OpeningPrefundBalance,
                now,
                userId,
                cancellationToken);

            var branch = await _dbContext.PartnerBranches
                .FirstOrDefaultAsync(item => item.TenantId == tenantId
                                             && item.PartnerId == partnerId
                                             && item.Name == seed.BranchName,
                    cancellationToken);

            var metadataJson = JsonSerializer.Serialize(new
            {
                timezone = "Africa/Lagos",
                supportsBillPay = true,
                settlementWindow = "T+0"
            });

            if (branch == null)
            {
                branch = new PartnerBranch
                {
                    Id = seed.BranchId,
                    TenantId = tenantId,
                    PartnerId = partnerId,
                    Name = seed.BranchName,
                    Country = seed.CountryCode,
                    City = seed.City,
                    MetadataJson = metadataJson,
                    CreatedAt = now,
                    CreatedBy = userId
                };
                _dbContext.PartnerBranches.Add(branch);
            }
            else
            {
                branch.Name = seed.BranchName;
                branch.Country = seed.CountryCode;
                branch.City = seed.City;
                branch.MetadataJson = metadataJson;
                branch.UpdatedAt = now;
                branch.UpdatedBy = userId;
            }

            var connector = await _dbContext.Connectors
                .FirstOrDefaultAsync(item => item.TenantId == tenantId
                                             && item.PartnerId == partnerId
                                             && item.ConnectorType == "API",
                    cancellationToken);

            var connectorConfigJson = JsonSerializer.Serialize(new
            {
                endpoint = $"https://api.{seed.CountryCode.ToLowerInvariant()}.demo.aonik/connectors/billpay",
                retryPolicy = "ExponentialBackoff",
                timeoutSeconds = 30
            });

            if (connector == null)
            {
                connector = new Connector
                {
                    Id = seed.ConnectorId,
                    TenantId = tenantId,
                    PartnerId = partnerId,
                    ConnectorType = "API",
                    CredentialsRef = $"kv://demo/partners/{seed.CountryCode.ToLowerInvariant()}/api",
                    ConfigJson = connectorConfigJson,
                    Status = "Active",
                    CreatedAt = now,
                    CreatedBy = userId
                };
                _dbContext.Connectors.Add(connector);
            }
            else
            {
                connector.ConnectorType = "API";
                connector.CredentialsRef = $"kv://demo/partners/{seed.CountryCode.ToLowerInvariant()}/api";
                connector.ConfigJson = connectorConfigJson;
                connector.Status = "Active";
                connector.UpdatedAt = now;
                connector.UpdatedBy = userId;
            }

            var connectorId = connector.Id;

            var routingRule = await _dbContext.RoutingRules
                .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == seed.RoutingRuleId, cancellationToken);

            var conditionsJson = JsonSerializer.Serialize(new
            {
                quoteContext = "BillPayment",
                capability = "BILLPAY",
                destinationCountry = seed.CountryCode
            });

            if (routingRule == null)
            {
                routingRule = new RoutingRule
                {
                    Id = seed.RoutingRuleId,
                    TenantId = tenantId,
                    ConditionsJson = conditionsJson,
                    TargetPartnerId = partnerId,
                    TargetConnectorId = connectorId,
                    Priority = seed.Priority,
                    IsActive = true,
                    CreatedAt = now,
                    CreatedBy = userId
                };
                _dbContext.RoutingRules.Add(routingRule);
            }
            else
            {
                routingRule.ConditionsJson = conditionsJson;
                routingRule.TargetPartnerId = partnerId;
                routingRule.TargetConnectorId = connectorId;
                routingRule.Priority = seed.Priority;
                routingRule.IsActive = true;
                routingRule.UpdatedAt = now;
                routingRule.UpdatedBy = userId;
            }

            partnerIdsByCountry[seed.CountryCode] = partnerId;
            connectorIdsByCountry[seed.CountryCode] = connectorId;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        operations.Add("Seeded cross-border partner network and routing rules");

        return (partnerIdsByCountry, connectorIdsByCountry);
    }

    private async Task<(IReadOnlyList<Guid> CategoryIds, IReadOnlyList<Guid> BillerIds, IReadOnlyList<Guid> ServiceIds)> SeedCrossBorderCatalogAsync(
        Guid tenantId,
        (IReadOnlyDictionary<string, Guid> PartnerIdsByCountry, IReadOnlyDictionary<string, Guid> ConnectorIdsByCountry) partnerNetwork,
        List<string> operations,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var userId = _currentUserProvider.GetCurrentUserId();

        if (!partnerNetwork.PartnerIdsByCountry.TryGetValue("GH", out var ghPartnerId))
        {
            throw new InvalidOperationException("Cross-border partner for GH is required.");
        }

        if (!partnerNetwork.PartnerIdsByCountry.TryGetValue("NG", out var ngPartnerId))
        {
            throw new InvalidOperationException("Cross-border partner for NG is required.");
        }

        if (!partnerNetwork.PartnerIdsByCountry.TryGetValue("KE", out var kePartnerId))
        {
            throw new InvalidOperationException("Cross-border partner for KE is required.");
        }

        if (!partnerNetwork.PartnerIdsByCountry.TryGetValue("ZA", out var zaPartnerId))
        {
            throw new InvalidOperationException("Cross-border partner for ZA is required.");
        }

        var ghCategoryId = await UpsertCategoryAsync(
            tenantId,
            UtilitiesCategoryId,
            "GH",
            "Utilities",
            "Electricity and water billers",
            1,
            now,
            userId,
            operations,
            cancellationToken);

        var ngCategoryId = await UpsertCategoryAsync(
            tenantId,
            NigeriaUtilitiesCategoryId,
            "NG",
            "Utilities",
            "Electricity and water billers in Nigeria",
            1,
            now,
            userId,
            operations,
            cancellationToken);

        var keCategoryId = await UpsertCategoryAsync(
            tenantId,
            KenyaUtilitiesCategoryId,
            "KE",
            "Utilities",
            "Electricity billers in Kenya",
            1,
            now,
            userId,
            operations,
            cancellationToken);

        var zaCategoryId = await UpsertCategoryAsync(
            tenantId,
            SouthAfricaUtilitiesCategoryId,
            "ZA",
            "Utilities",
            "Electricity billers in South Africa",
            1,
            now,
            userId,
            operations,
            cancellationToken);

        var ecgBillerId = await UpsertBillerAsync(
            tenantId,
            ghCategoryId,
            EcgBillerId,
            "ECG Power",
            "Ghana's electricity provider.",
            now,
            userId,
            operations,
            cancellationToken,
            ghPartnerId,
            "GH");

        var ghWaterBillerId = await UpsertBillerAsync(
            tenantId,
            ghCategoryId,
            GhanaWaterBillerId,
            "Ghana Water",
            "National water utility.",
            now,
            userId,
            operations,
            cancellationToken,
            ghPartnerId,
            "GH");

        var ikejaBillerId = await UpsertBillerAsync(
            tenantId,
            ngCategoryId,
            IkejaElectricBillerId,
            "Ikeja Electric",
            "Prepaid electricity in Lagos.",
            now,
            userId,
            operations,
            cancellationToken,
            ngPartnerId,
            "NG");

        var lagosWaterBillerId = await UpsertBillerAsync(
            tenantId,
            ngCategoryId,
            LagosWaterBillerId,
            "Lagos Water Board",
            "Postpaid water services for Lagos residents.",
            now,
            userId,
            operations,
            cancellationToken,
            ngPartnerId,
            "NG");

        var kenyaPowerBillerId = await UpsertBillerAsync(
            tenantId,
            keCategoryId,
            KenyaPowerBillerId,
            "Kenya Power",
            "National electricity distribution utility.",
            now,
            userId,
            operations,
            cancellationToken,
            kePartnerId,
            "KE");

        var cityPowerBillerId = await UpsertBillerAsync(
            tenantId,
            zaCategoryId,
            CityPowerBillerId,
            "City Power Johannesburg",
            "Municipal electricity provider.",
            now,
            userId,
            operations,
            cancellationToken,
            zaPartnerId,
            "ZA");

        var ecgServiceId = await UpsertServiceAsync(
            tenantId,
            ecgBillerId,
            EcgPrepaidServiceId,
            "BILLPAY.ELECTRICITY.PREPAID",
            "ECG Prepaid Electricity",
            "Prepaid",
            "GHS",
            5,
            500,
            true,
            true,
            BuildServiceFieldsJson(new[]
            {
                new CatalogServiceField("meterNumber", "Meter number", "text", true, 6, 16, null, "Enter meter number", null),
                new CatalogServiceField("customerName", "Customer name", "text", true, 2, 80, null, "Enter customer name", null)
            }),
            JsonSerializer.Serialize(new CatalogServiceValidation(
                $"/catalog/billers/{EcgBillerId}/services/{EcgPrepaidServiceId}/validate",
                "precheck")),
            operations,
            cancellationToken);

        var ghWaterServiceId = await UpsertServiceAsync(
            tenantId,
            ghWaterBillerId,
            GhanaWaterServiceId,
            "BILLPAY.WATER.POSTPAID",
            "Ghana Water Postpaid",
            "Postpaid",
            "GHS",
            10,
            1000,
            false,
            false,
            BuildServiceFieldsJson(new[]
            {
                new CatalogServiceField("accountNumber", "Account number", "text", true, 6, 20, null, "Enter account number", null)
            }),
            null,
            operations,
            cancellationToken);

        var ikejaServiceId = await UpsertServiceAsync(
            tenantId,
            ikejaBillerId,
            IkejaPrepaidServiceId,
            "BILLPAY.ELECTRICITY.PREPAID.NG.IKEJA",
            "Ikeja Prepaid Electricity",
            "Prepaid",
            "NGN",
            500,
            250000,
            true,
            true,
            BuildServiceFieldsJson(new[]
            {
                new CatalogServiceField("meterNumber", "Meter number", "text", true, 6, 16, null, "Enter meter number", null),
                new CatalogServiceField("customerName", "Customer name", "text", true, 2, 80, null, "Enter customer name", null)
            }),
            JsonSerializer.Serialize(new CatalogServiceValidation(
                $"/catalog/billers/{IkejaElectricBillerId}/services/{IkejaPrepaidServiceId}/validate",
                "precheck")),
            operations,
            cancellationToken);

        var ikejaPostpaidServiceId = await UpsertServiceAsync(
            tenantId,
            ikejaBillerId,
            IkejaPostpaidServiceId,
            "BILLPAY.ELECTRICITY.POSTPAID.NG.IKEJA",
            "Ikeja Postpaid Electricity",
            "Postpaid",
            "NGN",
            1000,
            400000,
            false,
            true,
            BuildServiceFieldsJson(new[]
            {
                new CatalogServiceField("accountNumber", "Account number", "text", true, 8, 20, null, "Enter account number", null),
                new CatalogServiceField("customerName", "Customer name", "text", true, 2, 80, null, "Enter customer name", null)
            }),
            JsonSerializer.Serialize(new CatalogServiceValidation(
                $"/catalog/billers/{IkejaElectricBillerId}/services/{IkejaPostpaidServiceId}/validate",
                "precheck")),
            operations,
            cancellationToken);

        var lagosWaterServiceId = await UpsertServiceAsync(
            tenantId,
            lagosWaterBillerId,
            LagosWaterServiceId,
            "BILLPAY.WATER.POSTPAID.NG.LAGOS",
            "Lagos Water Postpaid",
            "Postpaid",
            "NGN",
            1000,
            150000,
            false,
            false,
            BuildServiceFieldsJson(new[]
            {
                new CatalogServiceField("accountNumber", "Account number", "text", true, 8, 20, null, "Enter account number", null)
            }),
            null,
            operations,
            cancellationToken);

        var lagosWaterPrepaidServiceId = await UpsertServiceAsync(
            tenantId,
            lagosWaterBillerId,
            LagosWaterPrepaidServiceId,
            "BILLPAY.WATER.PREPAID.NG.LAGOS",
            "Lagos Water Prepaid",
            "Prepaid",
            "NGN",
            500,
            150000,
            true,
            true,
            BuildServiceFieldsJson(new[]
            {
                new CatalogServiceField("meterNumber", "Meter number", "text", true, 6, 16, null, "Enter meter number", null),
                new CatalogServiceField("customerName", "Customer name", "text", true, 2, 80, null, "Enter customer name", null)
            }),
            JsonSerializer.Serialize(new CatalogServiceValidation(
                $"/catalog/billers/{LagosWaterBillerId}/services/{LagosWaterPrepaidServiceId}/validate",
                "precheck")),
            operations,
            cancellationToken);

        var kenyaPowerServiceId = await UpsertServiceAsync(
            tenantId,
            kenyaPowerBillerId,
            KenyaPowerServiceId,
            "BILLPAY.ELECTRICITY.PREPAID.KE.KPLC",
            "Kenya Power Prepaid",
            "Prepaid",
            "KES",
            100,
            150000,
            true,
            true,
            BuildServiceFieldsJson(new[]
            {
                new CatalogServiceField("meterNumber", "Meter number", "text", true, 6, 16, null, "Enter meter number", null),
                new CatalogServiceField("nationalId", "National ID", "text", true, 6, 12, null, "Enter national ID", null)
            }),
            JsonSerializer.Serialize(new CatalogServiceValidation(
                $"/catalog/billers/{KenyaPowerBillerId}/services/{KenyaPowerServiceId}/validate",
                "precheck")),
            operations,
            cancellationToken);

        var kenyaPowerPostpaidServiceId = await UpsertServiceAsync(
            tenantId,
            kenyaPowerBillerId,
            KenyaPowerPostpaidServiceId,
            "BILLPAY.ELECTRICITY.POSTPAID.KE.KPLC",
            "Kenya Power Postpaid",
            "Postpaid",
            "KES",
            250,
            200000,
            false,
            true,
            BuildServiceFieldsJson(new[]
            {
                new CatalogServiceField("accountNumber", "Account number", "text", true, 8, 20, null, "Enter account number", null),
                new CatalogServiceField("nationalId", "National ID", "text", true, 6, 12, null, "Enter national ID", null)
            }),
            JsonSerializer.Serialize(new CatalogServiceValidation(
                $"/catalog/billers/{KenyaPowerBillerId}/services/{KenyaPowerPostpaidServiceId}/validate",
                "precheck")),
            operations,
            cancellationToken);

        var cityPowerServiceId = await UpsertServiceAsync(
            tenantId,
            cityPowerBillerId,
            CityPowerServiceId,
            "BILLPAY.ELECTRICITY.PREPAID.ZA.CPJ",
            "City Power Prepaid",
            "Prepaid",
            "ZAR",
            10,
            25000,
            true,
            true,
            BuildServiceFieldsJson(new[]
            {
                new CatalogServiceField("meterNumber", "Meter number", "text", true, 6, 16, null, "Enter meter number", null),
                new CatalogServiceField("surname", "Surname", "text", true, 2, 80, null, "Enter surname", null)
            }),
            JsonSerializer.Serialize(new CatalogServiceValidation(
                $"/catalog/billers/{CityPowerBillerId}/services/{CityPowerServiceId}/validate",
                "precheck")),
            operations,
            cancellationToken);

        var cityPowerPostpaidServiceId = await UpsertServiceAsync(
            tenantId,
            cityPowerBillerId,
            CityPowerPostpaidServiceId,
            "BILLPAY.ELECTRICITY.POSTPAID.ZA.CPJ",
            "City Power Postpaid",
            "Postpaid",
            "ZAR",
            50,
            50000,
            false,
            true,
            BuildServiceFieldsJson(new[]
            {
                new CatalogServiceField("accountNumber", "Account number", "text", true, 8, 20, null, "Enter account number", null),
                new CatalogServiceField("surname", "Surname", "text", true, 2, 80, null, "Enter surname", null)
            }),
            JsonSerializer.Serialize(new CatalogServiceValidation(
                $"/catalog/billers/{CityPowerBillerId}/services/{CityPowerPostpaidServiceId}/validate",
                "precheck")),
            operations,
            cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
        operations.Add("Extended catalog for NG, GH, KE, and ZA bill collection corridors");

        return (
            new[] { ghCategoryId, ngCategoryId, keCategoryId, zaCategoryId },
            new[] { ecgBillerId, ghWaterBillerId, ikejaBillerId, lagosWaterBillerId, kenyaPowerBillerId, cityPowerBillerId },
            new[]
            {
                ecgServiceId,
                ghWaterServiceId,
                ikejaServiceId,
                ikejaPostpaidServiceId,
                lagosWaterServiceId,
                lagosWaterPrepaidServiceId,
                kenyaPowerServiceId,
                kenyaPowerPostpaidServiceId,
                cityPowerServiceId,
                cityPowerPostpaidServiceId
            });
    }

    private sealed record DemoPersonSeed(
        Guid PartyId,
        string DisplayName,
        string Email,
        string Phone,
        string CountryCode,
        string CustomerTier,
        string FirstName,
        string LastName,
        string Nationality,
        string Occupation,
        string AddressLine1,
        string City,
        string State,
        string Postcode);

    private sealed record DemoBusinessSeed(
        Guid PartyId,
        string DisplayName,
        string Email,
        string Phone,
        string CountryCode,
        string CustomerTier,
        string RegistrationNumber,
        string Industry,
        string AddressLine1,
        string City,
        string State,
        string Postcode);

    private sealed record DemoRelationshipSeed(
        Guid RelationshipId,
        Guid FromPartyId,
        Guid ToPartyId,
        string RelationshipTypeCode,
        string Notes);

    private async Task<(IReadOnlyList<Guid> PartyIds, IReadOnlyList<Guid> RelationshipIds)> SeedCrossBorderPartiesAsync(
        Guid tenantId,
        List<string> operations,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                return await SeedCrossBorderPartiesCoreAsync(tenantId, operations, cancellationToken);
            }
            catch (DbUpdateConcurrencyException ex) when (attempt == 1)
            {
                if (_dbContext is DbContext dbContext)
                {
                    dbContext.ChangeTracker.Clear();
                    continue;
                }

                foreach (var entry in ex.Entries)
                {
                    if (entry.State == EntityState.Added)
                    {
                        entry.State = EntityState.Detached;
                        continue;
                    }

                    if (entry.State is EntityState.Modified or EntityState.Deleted)
                    {
                        await entry.ReloadAsync(cancellationToken);
                    }
                }
            }
        }

        throw new InvalidOperationException("Unable to seed cross-border parties after retry.");
    }

    private async Task<(IReadOnlyList<Guid> PartyIds, IReadOnlyList<Guid> RelationshipIds)> SeedCrossBorderPartiesCoreAsync(
        Guid tenantId,
        List<string> operations,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var userId = _currentUserProvider.GetCurrentUserId();

        var personSeeds = new List<DemoPersonSeed>
        {
            new(TundePartyId, "Tunde Adebayo", "tunde.adebayo@mailinator.com", "+2348011110001", "NG", "Retail", "Tunde", "Adebayo", "NG", "Software Engineer", "5 Isaac John Street", "Lagos", "Lagos", "100271"),
            new(AdwoaPartyId, "Adwoa Ofori", "adwoa.ofori@mailinator.com", "+2332011110002", "GH", "SMB", "Adwoa", "Ofori", "GH", "Pharmacist", "19 Liberation Road", "Accra", "Greater Accra", "23334"),
            new(PeterPartyId, "Peter Mwangi", "peter.mwangi@mailinator.com", "+254711110003", "KE", "Retail", "Peter", "Mwangi", "KE", "Procurement Officer", "14 Ngong Road", "Nairobi", "Nairobi", "00100"),
            new(NalediPartyId, "Naledi Dlamini", "naledi.dlamini@mailinator.com", "+27711110004", "ZA", "Enterprise", "Naledi", "Dlamini", "ZA", "Finance Analyst", "28 Rivonia Road", "Johannesburg", "Gauteng", "2196"),
            new(AishaPartyId, "Aisha Bello", "aisha.bello@mailinator.com", "+234811110005", "NG", "Retail", "Aisha", "Bello", "NG", "Medical Doctor", "9 Gana Street", "Abuja", "FCT", "900271"),
            new(KofiPartyId, "Kofi Asante", "kofi.asante@mailinator.com", "+233241110006", "GH", "SMB", "Kofi", "Asante", "GH", "Accountant", "8 Castle Road", "Kumasi", "Ashanti", "00233"),
            new(OliviaPartyId, "Olivia Bennett", "olivia.bennett@mailinator.com", "+447700900101", "GB", "Enterprise", "Olivia", "Bennett", "GB", "Investment Manager", "120 Bishopsgate", "London", "England", "EC2M 3AB"),
            new(LiamPartyId, "Liam Okoro", "liam.okoro@mailinator.com", "+447700900202", "GB", "SMB", "Liam", "Okoro", "GB", "Operations Lead", "48 Canary Wharf", "London", "England", "E14 5AB")
        };

        var businessSeeds = new List<DemoBusinessSeed>
        {
            new(AcmeImportsPartyId, "Acme Imports Ltd", "acme.imports@mailinator.com", "+2348095551001", "NG", "SMB", "RC-908771", "Logistics", "Plot 3 Wharf Road", "Apapa", "Lagos", "102272"),
            new(SafariFreightPartyId, "Safari Freight Co", "safari.freight@mailinator.com", "+2547015552002", "KE", "Enterprise", "PVT-557782", "Transportation", "31 Mombasa Road", "Nairobi", "Nairobi", "00506")
        };

        var partyIds = new List<Guid>();
        partyIds.Add(DemoPayerPartyId);
        partyIds.Add(DemoReceiverPartyId);

        foreach (var person in personSeeds)
        {
            var partyId = await UpsertPersonPartyAsync(tenantId, person, now, userId, cancellationToken);
            partyIds.Add(partyId);
        }

        foreach (var business in businessSeeds)
        {
            var partyId = await UpsertBusinessPartyAsync(tenantId, business, now, userId, cancellationToken);
            partyIds.Add(partyId);
        }

        var relationshipSeeds = new List<DemoRelationshipSeed>
        {
            new(DemoRelationshipId, DemoPayerPartyId, DemoReceiverPartyId, "Friend", "Demo relationship"),
            new(TundeAdwoaRelationshipId, TundePartyId, AdwoaPartyId, "Spouse", "Household transfer relationship"),
            new(TundePeterRelationshipId, TundePartyId, PeterPartyId, "Business", "Supplier payment relationship"),
            new(NalediAishaRelationshipId, NalediPartyId, AishaPartyId, "Sibling", "Family support relationship"),
            new(KofiAmaRelationshipId, KofiPartyId, DemoReceiverPartyId, "Child", "Family support relationship"),
            new(OliviaNalediRelationshipId, OliviaPartyId, NalediPartyId, "Business", "UK sender relationship to ZA payee"),
            new(LiamKwameRelationshipId, LiamPartyId, DemoPayerPartyId, "Sibling", "UK sender relationship to NG payer")
        };

        var relationshipIds = new List<Guid>();

        foreach (var relationship in relationshipSeeds)
        {
            var relationshipId = await UpsertRelationshipAsync(tenantId, relationship, now, userId, cancellationToken);
            relationshipIds.Add(relationshipId);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        operations.Add("Seeded UK-Africa customers with mailinator contacts and relationship graph");

        return (partyIds, relationshipIds);
    }

    private async Task<Guid> UpsertPersonPartyAsync(
        Guid tenantId,
        DemoPersonSeed seed,
        DateTime now,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = seed.Email.Trim().ToLowerInvariant();

        var party = await _dbContext.Parties
            .Include(item => item.Contacts)
            .Include(item => item.Addresses)
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == seed.PartyId,
                cancellationToken);

        if (party == null)
        {
            party = await _dbContext.Parties
                .Include(item => item.Contacts)
                .Include(item => item.Addresses)
                .FirstOrDefaultAsync(item => item.TenantId == tenantId
                                             && item.Contacts.Any(contact => contact.Type == "Email" && contact.Value == normalizedEmail),
                    cancellationToken);
        }

        if (party == null)
        {
            party = new Party
            {
                Id = seed.PartyId,
                TenantId = tenantId,
                PartyType = PersonPartyType,
                DisplayName = seed.DisplayName,
                Status = "Active",
                CustomerTierCode = seed.CustomerTier,
                CreatedAt = now,
                CreatedBy = userId
            };
            _dbContext.Parties.Add(party);
        }
        else
        {
            party.PartyType = PersonPartyType;
            party.DisplayName = seed.DisplayName;
            party.Status = "Active";
            party.CustomerTierCode = seed.CustomerTier;
            party.UpdatedAt = now;
            party.UpdatedBy = userId;
        }

        await UpsertPartyContactsAsync(party, now, seed.Email, seed.Phone);
        await UpsertPartyAddressAsync(
            party,
            "Home",
            seed.AddressLine1,
            seed.City,
            seed.State,
            seed.Postcode,
            seed.CountryCode,
            now);
        await UpsertPersonProfileAsync(
            party.Id,
            seed.FirstName,
            seed.LastName,
            seed.CountryCode,
            now,
            userId,
            cancellationToken,
            seed.Nationality,
            seed.Occupation);
        await EnsureCustomerRoleAssignmentAsync(tenantId, party.Id, now, userId, cancellationToken);

        return party.Id;
    }

    private async Task<Guid> UpsertBusinessPartyAsync(
        Guid tenantId,
        DemoBusinessSeed seed,
        DateTime now,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = seed.Email.Trim().ToLowerInvariant();

        var party = await _dbContext.Parties
            .Include(item => item.Contacts)
            .Include(item => item.Addresses)
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == seed.PartyId,
                cancellationToken);

        if (party == null)
        {
            party = await _dbContext.Parties
                .Include(item => item.Contacts)
                .Include(item => item.Addresses)
                .FirstOrDefaultAsync(item => item.TenantId == tenantId
                                             && item.Contacts.Any(contact => contact.Type == "Email" && contact.Value == normalizedEmail),
                    cancellationToken);
        }

        if (party == null)
        {
            party = new Party
            {
                Id = seed.PartyId,
                TenantId = tenantId,
                PartyType = BusinessPartyType,
                DisplayName = seed.DisplayName,
                Status = "Active",
                CustomerTierCode = seed.CustomerTier,
                CreatedAt = now,
                CreatedBy = userId
            };
            _dbContext.Parties.Add(party);
        }
        else
        {
            party.PartyType = BusinessPartyType;
            party.DisplayName = seed.DisplayName;
            party.Status = "Active";
            party.CustomerTierCode = seed.CustomerTier;
            party.UpdatedAt = now;
            party.UpdatedBy = userId;
        }

        await UpsertPartyContactsAsync(party, now, seed.Email, seed.Phone);
        await UpsertPartyAddressAsync(
            party,
            "Business",
            seed.AddressLine1,
            seed.City,
            seed.State,
            seed.Postcode,
            seed.CountryCode,
            now);
        await UpsertBusinessProfileAsync(
            party.Id,
            seed.RegistrationNumber,
            seed.CountryCode,
            seed.Industry,
            now,
            userId,
            cancellationToken);
        await EnsureCustomerRoleAssignmentAsync(tenantId, party.Id, now, userId, cancellationToken);

        return party.Id;
    }

    private async Task EnsureCustomerRoleAssignmentAsync(
        Guid tenantId,
        Guid partyId,
        DateTime now,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        var assignment = await _dbContext.PartyRoleAssignments
            .FirstOrDefaultAsync(item =>
                item.TenantId == tenantId &&
                item.PartyId == partyId &&
                item.Role == PartyRoles.Customer &&
                item.ContextType == "Tenant" &&
                item.ContextId == tenantId,
                cancellationToken);

        if (assignment != null)
        {
            return;
        }

        _dbContext.PartyRoleAssignments.Add(new PartyRoleAssignment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PartyId = partyId,
            Role = PartyRoles.Customer,
            ContextType = "Tenant",
            ContextId = tenantId,
            CreatedAt = now,
            CreatedBy = userId
        });
    }

    private async Task UpsertPartyAddressAsync(
        Party party,
        string type,
        string line1,
        string city,
        string state,
        string postcode,
        string country,
        DateTime now)
    {
        var address = party.Addresses.FirstOrDefault(item => item.Type == type);
        if (address == null)
        {
            party.Addresses.Add(new PartyAddress
            {
                PartyId = party.Id,
                Type = type,
                Line1 = line1,
                City = city,
                State = state,
                Postcode = postcode,
                Country = country,
                CreatedAt = now
            });
            return;
        }

        address.Line1 = line1;
        address.City = city;
        address.State = state;
        address.Postcode = postcode;
        address.Country = country;
        address.UpdatedAt = now;
    }

    private async Task UpsertBusinessProfileAsync(
        Guid partyId,
        string registrationNumber,
        string incorporationCountry,
        string industry,
        DateTime now,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        var profile = await _dbContext.BusinessProfiles
            .FirstOrDefaultAsync(item => item.PartyId == partyId, cancellationToken);

        if (profile == null)
        {
            _dbContext.BusinessProfiles.Add(new BusinessProfile
            {
                PartyId = partyId,
                RegistrationNumber = registrationNumber,
                IncorporationCountry = incorporationCountry,
                Industry = industry,
                KybStatus = "Unverified",
                CreatedAt = now,
                CreatedBy = userId
            });
            return;
        }

        profile.RegistrationNumber = registrationNumber;
        profile.IncorporationCountry = incorporationCountry;
        profile.Industry = industry;
        profile.KybStatus = "Unverified";
        profile.UpdatedAt = now;
        profile.UpdatedBy = userId;
    }

    private async Task<Guid> UpsertRelationshipAsync(
        Guid tenantId,
        DemoRelationshipSeed seed,
        DateTime now,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        var relationship = await _dbContext.PartyRelationships
            .FirstOrDefaultAsync(item => item.TenantId == tenantId
                                         && item.FromPartyId == seed.FromPartyId
                                         && item.ToPartyId == seed.ToPartyId
                                         && item.RelationshipTypeCode == seed.RelationshipTypeCode,
                cancellationToken);

        if (relationship == null)
        {
            relationship = new PartyRelationship
            {
                Id = seed.RelationshipId,
                TenantId = tenantId,
                FromPartyId = seed.FromPartyId,
                ToPartyId = seed.ToPartyId,
                RelationshipTypeCode = seed.RelationshipTypeCode,
                IsActive = true,
                Notes = seed.Notes,
                CreatedAt = now,
                CreatedBy = userId
            };
            _dbContext.PartyRelationships.Add(relationship);
        }
        else
        {
            relationship.IsActive = true;
            relationship.Notes = seed.Notes;
            relationship.UpdatedAt = now;
            relationship.UpdatedBy = userId;
        }

        return relationship.Id;
    }

    private async Task<(IReadOnlyList<Guid> HouseholdIds, IReadOnlyList<Guid> HouseholdMemberIds)> SeedHouseholdsAsync(
        Guid tenantId,
        List<string> operations,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var userId = _currentUserProvider.GetCurrentUserId();

        var households = new List<(Guid HouseholdId, string Name, Guid MemberId, string Role, string PermissionsJson)>
        {
            (FamilyHouseholdId, "Mensah Household", FamilyHouseholdMemberId, "Owner", JsonSerializer.Serialize(new[] { "Bills.Manage", "Goals.Manage" })),
            (ProfessionalsHouseholdId, "Cross-Border Professionals", ProfessionalsHouseholdMemberId, "Member", JsonSerializer.Serialize(new[] { "Bills.View", "Budget.View" }))
        };

        var householdIds = new List<Guid>();
        var householdMemberIds = new List<Guid>();

        foreach (var seed in households)
        {
            var household = await _dbContext.Households
                .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Name == seed.Name, cancellationToken);

            if (household == null)
            {
                household = new Household
                {
                    Id = seed.HouseholdId,
                    TenantId = tenantId,
                    Name = seed.Name,
                    CreatedAt = now,
                    CreatedBy = userId
                };
                _dbContext.Households.Add(household);
            }
            else
            {
                household.Name = seed.Name;
                household.UpdatedAt = now;
                household.UpdatedBy = userId;
            }

            householdIds.Add(household.Id);

            if (!userId.HasValue)
            {
                continue;
            }

            var existingMember = await _dbContext.HouseholdMembers
                .FirstOrDefaultAsync(item => item.HouseholdId == household.Id && item.UserId == userId.Value, cancellationToken);

            if (existingMember == null)
            {
                existingMember = new HouseholdMember
                {
                    Id = seed.MemberId,
                    HouseholdId = household.Id,
                    UserId = userId.Value,
                    Role = seed.Role,
                    PermissionsJson = seed.PermissionsJson,
                    CreatedAt = now,
                    CreatedBy = userId
                };
                _dbContext.HouseholdMembers.Add(existingMember);
            }
            else
            {
                existingMember.Role = seed.Role;
                existingMember.PermissionsJson = seed.PermissionsJson;
                existingMember.UpdatedAt = now;
                existingMember.UpdatedBy = userId;
            }

            householdMemberIds.Add(existingMember.Id);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        operations.Add("Seeded household groups for personal finance demos");

        return (householdIds, householdMemberIds);
    }

    private async Task<(IReadOnlyList<Guid> FxQuoteIds, IReadOnlyList<Guid> FeePolicyIds, IReadOnlyList<Guid> LimitsPolicyIds)> SeedCrossBorderPricingAsync(
        Guid tenantId,
        List<string> operations,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var userId = _currentUserProvider.GetCurrentUserId();

        var fxQuoteIds = new List<Guid>
        {
            await UpsertFxQuoteAsync(tenantId, DemoFxQuoteId, "NGN", "GHS", 0.0076m, "DemoRate", JsonSerializer.Serialize(new { corridor = "NG-GH" }), now, userId, cancellationToken),
            await UpsertFxQuoteAsync(tenantId, NgnKesFxQuoteId, "NGN", "KES", 0.083m, "DemoRate", JsonSerializer.Serialize(new { corridor = "NG-KE" }), now, userId, cancellationToken),
            await UpsertFxQuoteAsync(tenantId, NgnZarFxQuoteId, "NGN", "ZAR", 0.011m, "DemoRate", JsonSerializer.Serialize(new { corridor = "NG-ZA" }), now, userId, cancellationToken),
            await UpsertFxQuoteAsync(tenantId, UsdGhsFxQuoteId, "USD", "GHS", 12.85m, "DemoRate", JsonSerializer.Serialize(new { corridor = "USD-GH" }), now, userId, cancellationToken),
            await UpsertFxQuoteAsync(tenantId, UsdKesFxQuoteId, "USD", "KES", 150.40m, "DemoRate", JsonSerializer.Serialize(new { corridor = "USD-KE" }), now, userId, cancellationToken),
            await UpsertFxQuoteAsync(tenantId, UsdZarFxQuoteId, "USD", "ZAR", 18.60m, "DemoRate", JsonSerializer.Serialize(new { corridor = "USD-ZA" }), now, userId, cancellationToken),
            await UpsertFxQuoteAsync(tenantId, GbpNgnFxQuoteId, "GBP", "NGN", 1985.25m, "DemoRate", JsonSerializer.Serialize(new { corridor = "UK-NG" }), now, userId, cancellationToken),
            await UpsertFxQuoteAsync(tenantId, GbpGhsFxQuoteId, "GBP", "GHS", 16.78m, "DemoRate", JsonSerializer.Serialize(new { corridor = "UK-GH" }), now, userId, cancellationToken),
            await UpsertFxQuoteAsync(tenantId, GbpKesFxQuoteId, "GBP", "KES", 168.45m, "DemoRate", JsonSerializer.Serialize(new { corridor = "UK-KE" }), now, userId, cancellationToken),
            await UpsertFxQuoteAsync(tenantId, GbpZarFxQuoteId, "GBP", "ZAR", 24.31m, "DemoRate", JsonSerializer.Serialize(new { corridor = "UK-ZA" }), now, userId, cancellationToken)
        };

        var breakdown = new List<FeeBreakdownDefinition>
        {
            new("SERVICE_FEE", "Service fee", "Fixed"),
            new("FX_MARKUP", "FX markup", "FxMarkup")
        };

        var feePolicyIds = new List<Guid>
        {
            await UpsertFeePolicyAsync(
                tenantId,
                CrossBorderBand1FeePolicyId,
                "CrossBorder-NG-GH-Band-001-100",
                40m,
                0.010m,
                new FeePolicyConditions("BILLPAY.ELECTRICITY.PREPAID", "NG", "GH", "NGN", "GHS", "Retail", 0m, 100m, 20m, 180m, 120, "DemoRate", "AwayFromZero", breakdown),
                now,
                userId,
                cancellationToken),
            await UpsertFeePolicyAsync(
                tenantId,
                CrossBorderBand2FeePolicyId,
                "CrossBorder-NG-GH-Band-100-1000",
                85m,
                0.012m,
                new FeePolicyConditions("BILLPAY.ELECTRICITY.PREPAID", "NG", "GH", "NGN", "GHS", "Retail", 100.01m, 1000m, 35m, 400m, 150, "DemoRate", "AwayFromZero", breakdown),
                now,
                userId,
                cancellationToken),
            await UpsertFeePolicyAsync(
                tenantId,
                CrossBorderBand3FeePolicyId,
                "CrossBorder-NG-GH-Band-1000-Plus",
                150m,
                0.015m,
                new FeePolicyConditions("BILLPAY.ELECTRICITY.PREPAID", "NG", "GH", "NGN", "GHS", "Retail", 1000.01m, null, 60m, 1250m, 180, "DemoRate", "AwayFromZero", breakdown),
                now,
                userId,
                cancellationToken),
            await UpsertFeePolicyAsync(
                tenantId,
                CrossBorderKesFeePolicyId,
                "CrossBorder-NG-KE-Default",
                75m,
                0.013m,
                new FeePolicyConditions("BILLPAY.ELECTRICITY.PREPAID.KE.KPLC", "NG", "KE", "NGN", "KES", "Retail", null, null, 30m, 600m, 140, "DemoRate", "AwayFromZero", breakdown),
                now,
                userId,
                cancellationToken),
            await UpsertFeePolicyAsync(
                tenantId,
                CrossBorderZarFeePolicyId,
                "CrossBorder-NG-ZA-Default",
                90m,
                0.014m,
                new FeePolicyConditions("BILLPAY.ELECTRICITY.PREPAID.ZA.CPJ", "NG", "ZA", "NGN", "ZAR", "Retail", null, null, 40m, 750m, 150, "DemoRate", "AwayFromZero", breakdown),
                now,
                userId,
                cancellationToken)
        };

        var limitsPolicyIds = new List<Guid>
        {
            await UpsertLimitsPolicyAsync(tenantId, DemoLimitsPolicyId, "NGN", 5000000m, "Daily", now, userId, cancellationToken),
            await UpsertLimitsPolicyAsync(tenantId, KenyaLimitsPolicyId, "KES", 300000m, "Daily", now, userId, cancellationToken),
            await UpsertLimitsPolicyAsync(tenantId, SouthAfricaLimitsPolicyId, "ZAR", 120000m, "Daily", now, userId, cancellationToken)
        };

        await _dbContext.SaveChangesAsync(cancellationToken);
        operations.Add("Seeded UK-to-Africa FX quotes and tiered cross-border charging policies");

        return (fxQuoteIds, feePolicyIds, limitsPolicyIds);
    }

    private async Task<Guid> UpsertFxQuoteAsync(
        Guid tenantId,
        Guid fxQuoteId,
        string baseCurrency,
        string targetCurrency,
        decimal rate,
        string provider,
        string metadataJson,
        DateTime now,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        var fxQuote = await _dbContext.FxQuotes
            .FirstOrDefaultAsync(item => item.TenantId == tenantId
                                         && item.BaseCurrency == baseCurrency
                                         && item.TargetCurrency == targetCurrency
                                         && item.Provider == provider,
                cancellationToken);

        if (fxQuote == null)
        {
            fxQuote = new FxQuote
            {
                Id = fxQuoteId,
                TenantId = tenantId,
                BaseCurrency = baseCurrency,
                TargetCurrency = targetCurrency,
                Rate = rate,
                ExpiresAt = now.AddHours(24),
                Provider = provider,
                MetadataJson = metadataJson,
                CreatedAt = now,
                CreatedBy = userId
            };
            _dbContext.FxQuotes.Add(fxQuote);
        }
        else
        {
            fxQuote.BaseCurrency = baseCurrency;
            fxQuote.TargetCurrency = targetCurrency;
            fxQuote.Rate = rate;
            fxQuote.ExpiresAt = now.AddHours(24);
            fxQuote.Provider = provider;
            fxQuote.MetadataJson = metadataJson;
            fxQuote.UpdatedAt = now;
            fxQuote.UpdatedBy = userId;
        }

        return fxQuote.Id;
    }

    private async Task<Guid> UpsertFeePolicyAsync(
        Guid tenantId,
        Guid feePolicyId,
        string name,
        decimal fixedFee,
        decimal percentageFee,
        FeePolicyConditions conditions,
        DateTime now,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        var feePolicy = await _dbContext.FeePolicies
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Name == name, cancellationToken);

        var conditionsJson = JsonSerializer.Serialize(conditions);

        if (feePolicy == null)
        {
            feePolicy = new FeePolicy
            {
                Id = feePolicyId,
                TenantId = tenantId,
                Name = name,
                FixedFee = fixedFee,
                PercentageFee = percentageFee,
                ConditionsJson = conditionsJson,
                IsActive = true,
                CreatedAt = now,
                CreatedBy = userId
            };
            _dbContext.FeePolicies.Add(feePolicy);
        }
        else
        {
            feePolicy.Name = name;
            feePolicy.FixedFee = fixedFee;
            feePolicy.PercentageFee = percentageFee;
            feePolicy.ConditionsJson = conditionsJson;
            feePolicy.IsActive = true;
            feePolicy.UpdatedAt = now;
            feePolicy.UpdatedBy = userId;
        }

        return feePolicy.Id;
    }

    private async Task<Guid> UpsertLimitsPolicyAsync(
        Guid tenantId,
        Guid limitsPolicyId,
        string currency,
        decimal maxAmount,
        string period,
        DateTime now,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        var limitsPolicy = await _dbContext.LimitsPolicies
            .FirstOrDefaultAsync(item => item.TenantId == tenantId
                                         && item.ScopeType == "Tenant"
                                         && item.ScopeId == tenantId
                                         && item.Currency == currency
                                         && item.Period == period,
                cancellationToken);

        if (limitsPolicy == null)
        {
            limitsPolicy = new LimitsPolicy
            {
                Id = limitsPolicyId,
                TenantId = tenantId,
                ScopeType = "Tenant",
                ScopeId = tenantId,
                Currency = currency,
                MaxAmount = maxAmount,
                Period = period,
                IsActive = true,
                CreatedAt = now,
                CreatedBy = userId
            };
            _dbContext.LimitsPolicies.Add(limitsPolicy);
        }
        else
        {
            limitsPolicy.ScopeType = "Tenant";
            limitsPolicy.ScopeId = tenantId;
            limitsPolicy.Currency = currency;
            limitsPolicy.MaxAmount = maxAmount;
            limitsPolicy.Period = period;
            limitsPolicy.IsActive = true;
            limitsPolicy.UpdatedAt = now;
            limitsPolicy.UpdatedBy = userId;
        }

        return limitsPolicy.Id;
    }

    private async Task UpsertCrossBorderMarkerAsync(
        Guid tenantId,
        string seedType,
        (Guid UtilitiesCategoryId, Guid EcgBillerId, Guid WaterBillerId, Guid EcgServiceId, Guid WaterServiceId) billCollectionCatalog,
        (Guid PayerPartyId, Guid ReceiverPartyId, Guid RelationshipId) billCollectionParties,
        (Guid FxQuoteId, Guid FeePolicyId, Guid LimitsPolicyId) billCollectionPricing,
        (IReadOnlyList<Guid> CountryIds, IReadOnlyList<Guid> CurrencyIds) tenantCoverage,
        (IReadOnlyDictionary<string, Guid> PartnerIdsByCountry, IReadOnlyDictionary<string, Guid> ConnectorIdsByCountry) partnerNetwork,
        (IReadOnlyList<Guid> CategoryIds, IReadOnlyList<Guid> BillerIds, IReadOnlyList<Guid> ServiceIds) crossBorderCatalog,
        (IReadOnlyList<Guid> PartyIds, IReadOnlyList<Guid> RelationshipIds) crossBorderParties,
        (IReadOnlyList<Guid> HouseholdIds, IReadOnlyList<Guid> HouseholdMemberIds) households,
        (IReadOnlyList<Guid> FxQuoteIds, IReadOnlyList<Guid> FeePolicyIds, IReadOnlyList<Guid> LimitsPolicyIds) crossBorderPricing,
        List<string> operations,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var userId = _currentUserProvider.GetCurrentUserId();
        var payload = new
        {
            TenantId = tenantId,
            SeedType = seedType,
            BillCollection = new
            {
                billCollectionCatalog.UtilitiesCategoryId,
                billCollectionCatalog.EcgBillerId,
                billCollectionCatalog.WaterBillerId,
                billCollectionCatalog.EcgServiceId,
                billCollectionCatalog.WaterServiceId,
                billCollectionParties.PayerPartyId,
                billCollectionParties.ReceiverPartyId,
                billCollectionParties.RelationshipId,
                billCollectionPricing.FxQuoteId,
                billCollectionPricing.FeePolicyId,
                billCollectionPricing.LimitsPolicyId
            },
            CrossBorder = new
            {
                CountryIds = tenantCoverage.CountryIds,
                CurrencyIds = tenantCoverage.CurrencyIds,
                PartnerIdsByCountry = partnerNetwork.PartnerIdsByCountry,
                ConnectorIdsByCountry = partnerNetwork.ConnectorIdsByCountry,
                CategoryIds = crossBorderCatalog.CategoryIds,
                BillerIds = crossBorderCatalog.BillerIds,
                ServiceIds = crossBorderCatalog.ServiceIds,
                PartyIds = crossBorderParties.PartyIds,
                RelationshipIds = crossBorderParties.RelationshipIds,
                HouseholdIds = households.HouseholdIds,
                HouseholdMemberIds = households.HouseholdMemberIds,
                FxQuoteIds = crossBorderPricing.FxQuoteIds,
                FeePolicyIds = crossBorderPricing.FeePolicyIds,
                LimitsPolicyIds = crossBorderPricing.LimitsPolicyIds
            }
        };

        var settingValue = JsonSerializer.Serialize(payload);
        var setting = await _dbContext.Settings
            .FirstOrDefaultAsync(item => item.Scope == SettingScope.Tenant
                                         && item.TenantId == tenantId
                                         && item.Key == CrossBorderDemoSeedKey,
                cancellationToken);

        if (setting == null)
        {
            setting = new Setting
            {
                Key = CrossBorderDemoSeedKey,
                Value = settingValue,
                Scope = SettingScope.Tenant,
                TenantId = tenantId,
                CreatedAt = now,
                CreatedBy = userId
            };
            _dbContext.Settings.Add(setting);
            operations.Add("Cross-border demo seed marker created");
        }
        else
        {
            setting.Value = settingValue;
            setting.UpdatedAt = now;
            setting.UpdatedBy = userId;
            operations.Add("Cross-border demo seed marker updated");
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsurePermissionAsync(string permissionKey, CancellationToken cancellationToken)
    {
        var userId = _currentUserProvider.GetCurrentUserId();
        if (!userId.HasValue)
        {
            throw new InvalidOperationException("Authenticated user is required.");
        }

        var hasPermission = await _permissionService.HasPermissionAsync(userId.Value, permissionKey, cancellationToken);
        if (!hasPermission)
        {
            throw new InvalidOperationException($"Permission {permissionKey} is required.");
        }
    }
}
