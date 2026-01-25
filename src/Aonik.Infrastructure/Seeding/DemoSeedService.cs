using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Aonik.Application.Abstractions.Multitenancy;
using Aonik.Application.Abstractions.Observability;
using Aonik.Application.Abstractions.Persistence;
using Aonik.Application.Models.Seeding;
using Aonik.Application.Services.Compliance;
using Aonik.Application.Services.Identity;
using Aonik.Application.Services.Seeding;
using Aonik.Infrastructure.Persistence.Seed;
using Aonik.SharedKernel.Abstractions;
using Aonik.Domain.Catalog.Entities;
using Aonik.Domain.Pricing.Entities;
using Aonik.Domain.Party.Entities;
using Aonik.Domain.Settings;
using Aonik.Domain.Settings.Entities;
using Aonik.Domain.Identity.Entities;
using Aonik.Application.Models.Catalog;
using Aonik.Application.Models.Pricing;
using System.Text.Json;

namespace Aonik.Infrastructure.Seeding;

public class DemoSeedService : IDemoSeedService
{
    private const string DemoSeedKey = "DemoSeed.BillPayment";
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
    private static readonly Guid DemoPayerPartyId = Guid.Parse("bfe9921e-2f3e-4c56-b8d1-4f5b2a7c3d44");
    private static readonly Guid DemoReceiverPartyId = Guid.Parse("2a3e1f59-44f7-4df4-a8f1-936f9d9d13cd");
    private static readonly Guid DemoRelationshipId = Guid.Parse("c90127f4-9b45-4a8e-9b90-7d0f3d4e65cc");
    private static readonly Guid DemoFxQuoteId = Guid.Parse("9a8d9f56-b91b-4d1a-8f7a-2e12a54e50e2");
    private static readonly Guid DemoFeePolicyId = Guid.Parse("7b6b3b5d-91b9-4d25-8f2c-ead45812c1a1");
    private static readonly Guid DemoLimitsPolicyId = Guid.Parse("5a8dd1d8-1f47-41f5-9e8d-1ef1e7c7880a");

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

    public async Task<DemoSeedResult> SeedAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Tenants.Write", cancellationToken);

        var tenantExists = await _dbContext.Tenants.AnyAsync(t => t.Id == tenantId, cancellationToken);
        if (!tenantExists)
        {
            throw new InvalidOperationException($"Tenant {tenantId} not found");
        }

        _tenantContext.TenantId = tenantId;
        _tenantContext.ResolutionSource = "AdminTenantAction";

        var operations = new List<string>();

        var identitySeed = new IdentitySeedService((IAonikDbContext)_dbContext, _loggerFactory.CreateLogger<IdentitySeedService>());
        await identitySeed.SeedAsync(cancellationToken);
        operations.Add("IdentitySeed");

        var catalogSeed = new CatalogSeedService((IAonikDbContext)_dbContext, _loggerFactory.CreateLogger<CatalogSeedService>());
        await catalogSeed.SeedAsync(cancellationToken);
        operations.Add("CatalogSeed");

        await EnsureTenantAdminRoleAsync(tenantId, operations, cancellationToken);
        var catalogIds = await SeedCatalogAsync(tenantId, operations, cancellationToken);
        var partyIds = await SeedPartiesAsync(tenantId, operations, cancellationToken);
        var pricingIds = await SeedPricingAsync(tenantId, operations, cancellationToken);
        await UpsertMarkerAsync(tenantId, catalogIds, partyIds, pricingIds, operations, cancellationToken);

        var now = _clock.UtcNow;
        var userId = _currentUserProvider.GetCurrentUserId();

        await _auditLogWriter.LogAsync(
            AuditEventNames.TenantDemoSeeded,
            "TenantDemoSeed",
            tenantId,
            tenantId,
            userId,
            _correlationContext.CorrelationId,
            System.Text.Json.JsonSerializer.Serialize(new { tenantId, operations }),
            cancellationToken);

        return new DemoSeedResult(tenantId, now, operations);
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

    private async Task<(Guid UtilitiesCategoryId, Guid EcgBillerId, Guid WaterBillerId, Guid EcgServiceId, Guid WaterServiceId)> SeedCatalogAsync(
        Guid tenantId,
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
        var ecgBillerId = await UpsertBillerAsync(tenantId, categoryId, EcgBillerId, "ECG Power", "Ghana's electricity provider.", now, userId, operations, cancellationToken);
        var waterBillerId = await UpsertBillerAsync(tenantId, categoryId, GhanaWaterBillerId, "Ghana Water", "National water utility.", now, userId, operations, cancellationToken);

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
        CancellationToken cancellationToken)
    {
        var biller = await _dbContext.CatalogBillers
            .FirstOrDefaultAsync(item => item.TenantId == tenantId
                                         && item.CountryCode == "GH"
                                         && item.Name == name,
                cancellationToken);

        if (biller == null)
        {
            biller = new CatalogBiller
            {
                Id = billerId,
                TenantId = tenantId,
                CategoryId = categoryId,
                CountryCode = "GH",
                Name = name,
                Description = description,
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
            biller.CountryCode = "GH";
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

        var payerEmail = "kwame.mensah@demo.aonik";
        var receiverEmail = "ama.boateng@demo.aonik";

        var payerParty = await _dbContext.Parties
            .Include(party => party.Contacts)
            .FirstOrDefaultAsync(party => party.TenantId == tenantId
                                          && party.Contacts.Any(contact => contact.Type == "Email" && contact.Value == payerEmail),
                cancellationToken);

        if (payerParty == null)
        {
            payerParty = new Party
            {
                Id = DemoPayerPartyId,
                TenantId = tenantId,
                PartyType = "Person",
                DisplayName = "Kwame Mensah",
                Status = "Active",
                CustomerTierCode = "Retail",
                CreatedAt = now,
                CreatedBy = userId
            };
            _dbContext.Parties.Add(payerParty);
            operations.Add("Seeded payer party");
        }

        await UpsertPartyContactsAsync(payerParty, now, payerEmail, "+234800000000");
        await UpsertPersonProfileAsync(payerParty.Id, "Kwame", "Mensah", "NG", now, userId, cancellationToken);

        var receiverParty = await _dbContext.Parties
            .Include(party => party.Contacts)
            .FirstOrDefaultAsync(party => party.TenantId == tenantId
                                          && party.Contacts.Any(contact => contact.Type == "Email" && contact.Value == receiverEmail),
                cancellationToken);

        if (receiverParty == null)
        {
            receiverParty = new Party
            {
                Id = DemoReceiverPartyId,
                TenantId = tenantId,
                PartyType = "Person",
                DisplayName = "Ama Boateng",
                Status = "Active",
                CreatedAt = now,
                CreatedBy = userId
            };
            _dbContext.Parties.Add(receiverParty);
            operations.Add("Seeded receiver party");
        }

        await UpsertPartyContactsAsync(receiverParty, now, receiverEmail, "+233200000000");
        await UpsertPersonProfileAsync(receiverParty.Id, "Ama", "Boateng", "GH", now, userId, cancellationToken);

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

    private async Task UpsertPartyContactsAsync(Party party, DateTime now, string email = "kwame.mensah@demo.aonik", string phone = "+234800000000")
    {
        var hasEmail = party.Contacts.Any(contact => contact.Type == "Email" && contact.Value == email);
        if (!hasEmail)
        {
            party.Contacts.Add(new PartyContact
            {
                PartyId = party.Id,
                Type = "Email",
                Value = email,
                IsPrimary = true,
                CreatedAt = now
            });
        }

        var hasPhone = party.Contacts.Any(contact => contact.Type == "Phone" && contact.Value == phone);
        if (!hasPhone)
        {
            party.Contacts.Add(new PartyContact
            {
                PartyId = party.Id,
                Type = "Phone",
                Value = phone,
                IsPrimary = false,
                CreatedAt = now
            });
        }
    }

    private async Task UpsertPersonProfileAsync(Guid partyId, string firstName, string lastName, string countryCode, DateTime now, Guid? userId, CancellationToken cancellationToken)
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
