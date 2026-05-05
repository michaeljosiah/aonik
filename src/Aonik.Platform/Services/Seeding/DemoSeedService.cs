using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Observability;
using Aonik.Platform.Persistence;
using Aonik.Platform.Contracts.Models.Seeding;
using Aonik.SharedKernel.Abstractions;
using Aonik.Platform.Contracts.Services.Seeding;
using Aonik.Platform.Entities.Party;
using PartyEntity = Aonik.Platform.Entities.Party.Party;
using Aonik.Platform.Entities.Settings;
using Aonik.Platform.Entities.Identity;
using Aonik.Platform.Services.Identity;
using Aonik.Finance.Persistence;
using Aonik.Agents.Persistence;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Aonik.Platform.Services.Seeding;

internal class DemoSeedService : IDemoSeedService
{
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> TenantSeedLocks = new();

    private const string PersonPartyType = "Person";
    private const string BusinessPartyType = "Business";

    private const string DemoSeedKey = "DemoSeed.BillPayment";
    private const string CrossBorderDemoSeedKey = "DemoSeed.CrossBorderPayments";

    private readonly PlatformDbContext _dbContext;
    private readonly IEnumerable<IDemoSeedContributor> _contributors;
    private readonly IClock _clock;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IAuditLogWriter _auditLogWriter;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ICorrelationContext _correlationContext;
    private readonly IPermissionService _permissionService;
    private readonly ITenantContext _tenantContext;
    private readonly FinanceDbContext _financeDbContext;
    private readonly AgentsDbContext _agentsDbContext;

    // ── Platform-only Guid constants ─────────────────────────────────
    //
    // All deterministic GUIDs live in platform-demo-ids.json (embedded
    // resource) and are exposed here as static field aliases so the rest
    // of the file reads naturally. The JSON is loaded once per process
    // via PlatformDemoSeedIds.Instance.

    private static readonly PlatformDemoSeedIds SeedIds = PlatformDemoSeedIds.Instance;

    private static readonly Guid DemoPayerPartyId = SeedIds.DemoPair.DemoPayerPartyId;
    private static readonly Guid DemoReceiverPartyId = SeedIds.DemoPair.DemoReceiverPartyId;
    private static readonly Guid DemoRelationshipId = SeedIds.DemoPair.DemoRelationshipId;

    private static readonly Guid TundePartyId = SeedIds.Personas.TundePartyId;
    private static readonly Guid AdwoaPartyId = SeedIds.Personas.AdwoaPartyId;
    private static readonly Guid PeterPartyId = SeedIds.Personas.PeterPartyId;
    private static readonly Guid NalediPartyId = SeedIds.Personas.NalediPartyId;
    private static readonly Guid AishaPartyId = SeedIds.Personas.AishaPartyId;
    private static readonly Guid KofiPartyId = SeedIds.Personas.KofiPartyId;
    private static readonly Guid AcmeImportsPartyId = SeedIds.Personas.AcmeImportsPartyId;
    private static readonly Guid SafariFreightPartyId = SeedIds.Personas.SafariFreightPartyId;
    private static readonly Guid OliviaPartyId = SeedIds.Personas.OliviaPartyId;
    private static readonly Guid LiamPartyId = SeedIds.Personas.LiamPartyId;

    private static readonly Guid TundeAdwoaRelationshipId = SeedIds.PersonaRelationships.TundeAdwoaRelationshipId;
    private static readonly Guid TundePeterRelationshipId = SeedIds.PersonaRelationships.TundePeterRelationshipId;
    private static readonly Guid NalediAishaRelationshipId = SeedIds.PersonaRelationships.NalediAishaRelationshipId;
    private static readonly Guid KofiAmaRelationshipId = SeedIds.PersonaRelationships.KofiAmaRelationshipId;
    private static readonly Guid OliviaNalediRelationshipId = SeedIds.PersonaRelationships.OliviaNalediRelationshipId;
    private static readonly Guid LiamKwameRelationshipId = SeedIds.PersonaRelationships.LiamKwameRelationshipId;
    // Canonical demo name lists are loaded from platform-demo-names.json
    // (embedded resource) so the seeder file isn't littered with literal arrays.
    private static readonly PlatformDemoSeedNames SeedNames = PlatformDemoSeedNames.Instance;

    private static readonly string[] DemoWorkflowSlugs = SeedNames.WorkflowSlugs;
    private static readonly string[] DemoNotificationTypes = SeedNames.NotificationTypes;
    private static readonly string[] DemoAgentNames = SeedNames.AgentNames;
    private static readonly string[] DemoPartnerNames = SeedNames.PartnerNames;
    private static readonly string[] DemoHouseholdNames = SeedNames.HouseholdNames;

    public DemoSeedService(
        PlatformDbContext dbContext,
        IEnumerable<IDemoSeedContributor> contributors,
        IClock clock,
        ILoggerFactory loggerFactory,
        IAuditLogWriter auditLogWriter,
        ICurrentUserProvider currentUserProvider,
        ICorrelationContext correlationContext,
        IPermissionService permissionService,
        ITenantContext tenantContext,
        FinanceDbContext financeDbContext,
        AgentsDbContext agentsDbContext)
    {
        _dbContext = dbContext;
        _contributors = contributors;
        _clock = clock;
        _loggerFactory = loggerFactory;
        _auditLogWriter = auditLogWriter;
        _currentUserProvider = currentUserProvider;
        _correlationContext = correlationContext;
        _permissionService = permissionService;
        _tenantContext = tenantContext;
        _financeDbContext = financeDbContext;
        _agentsDbContext = agentsDbContext;
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
            var seedContext = new DemoSeedContext(tenantId, normalizedSeedType, _clock.UtcNow, _currentUserProvider.GetCurrentUserId());

            // Phase 1: Identity seed
            var identitySeed = new IdentitySeedService(_dbContext, _loggerFactory.CreateLogger<IdentitySeedService>());
            await identitySeed.SeedAsync(cancellationToken);
            operations.Add("IdentitySeed");
            ClearTrackingIfSupported(_dbContext);

            // Phase 2: Catalog reference data (Platform-only)
            var catalogSeed = new CatalogSeedService(_dbContext, _loggerFactory.CreateLogger<CatalogSeedService>());
            await catalogSeed.SeedAsync(cancellationToken);
            operations.Add("CatalogSeed");
            ClearTrackingIfSupported(_dbContext);

            // Phase 3: Module catalog categories (biller categories via contributors)
            await SeedContributorsAsync(DemoSeedPhase.CatalogCategories, seedContext, operations, cancellationToken);
            ClearContributorTracking();

            // Phase 4: Tenant admin role
            await EnsureTenantAdminRoleAsync(tenantId, operations, cancellationToken);
            ClearTrackingIfSupported(_dbContext);

            // Phase 5: Bill collection partner (via contributors)
            await SeedContributorsAsync(DemoSeedPhase.BillCollectionPartner, seedContext, operations, cancellationToken);
            ClearTrackingIfSupported(_dbContext);
            ClearContributorTracking();

            // Phase 6: Demo catalog (via contributors)
            await SeedContributorsAsync(DemoSeedPhase.Catalog, seedContext, operations, cancellationToken);
            ClearTrackingIfSupported(_dbContext);

            // Phase 7: Parties (Platform-only)
            var partyIds = await SeedPartiesAsync(tenantId, operations, cancellationToken);
            ClearTrackingIfSupported(_dbContext);

            // Phase 8: Pricing (via contributors)
            await SeedContributorsAsync(DemoSeedPhase.Pricing, seedContext, operations, cancellationToken);
            ClearTrackingIfSupported(_dbContext);
            ClearContributorTracking();

            // Phase 8.5: Workflows (Agents module — seeds the agent fleet
            // referenced by the workflow rows, then the workflow registry).
            await SeedContributorsAsync(DemoSeedPhase.Workflows, seedContext, operations, cancellationToken);
            ClearTrackingIfSupported(_dbContext);
            ClearContributorTracking();

            // Phase 9: Seed marker
            await UpsertMarkerAsync(tenantId, partyIds, operations, cancellationToken);
            ClearTrackingIfSupported(_dbContext);

            if (normalizedSeedType == DemoSeedTypes.CrossBorderPayments)
            {
                var tenantSnapshot = await CaptureTenantSnapshotAsync(tenantId, cancellationToken);

                // Phase 10: UK home base
                await EnsureUkHomeBaseAsync(tenantId, operations, cancellationToken);
                ClearTrackingIfSupported(_dbContext);

                // Phase 11: Tenant coverage
                var tenantCoverage = await SeedCrossBorderTenantCoverageAsync(tenantId, operations, cancellationToken);
                ClearTrackingIfSupported(_dbContext);

                // Phase 12: Cross-border partner network (via contributors)
                await SeedContributorsAsync(DemoSeedPhase.CrossBorderPartnerNetwork, seedContext, operations, cancellationToken);
                ClearTrackingIfSupported(_dbContext);
                ClearContributorTracking();

                // Phase 13: Cross-border catalog (via contributors)
                await SeedContributorsAsync(DemoSeedPhase.CrossBorderCatalog, seedContext, operations, cancellationToken);
                ClearTrackingIfSupported(_dbContext);

                // Phase 14: Cross-border parties (Platform-only)
                var crossBorderParties = await SeedCrossBorderPartiesAsync(tenantId, operations, cancellationToken);
                ClearTrackingIfSupported(_dbContext);

                // Phase 15: Households (via contributors)
                await SeedContributorsAsync(DemoSeedPhase.Households, seedContext, operations, cancellationToken);
                ClearTrackingIfSupported(_dbContext);
                ClearContributorTracking();

                // Phase 16: Cross-border pricing (via contributors)
                await SeedContributorsAsync(DemoSeedPhase.CrossBorderPricing, seedContext, operations, cancellationToken);
                ClearTrackingIfSupported(_dbContext);
                ClearContributorTracking();

                // Phase 17: Cross-border seed marker
                await UpsertCrossBorderMarkerAsync(
                    tenantId,
                    normalizedSeedType,
                    tenantSnapshot,
                    partyIds,
                    tenantCoverage,
                    crossBorderParties,
                    operations,
                    cancellationToken);
            }

            // Phase 18: Activity — orders, agent runs, proposals,
            // notifications. Runs last so it can reference every seeded
            // entity (catalog, pricing, parties, workflows, partners,
            // households) regardless of seed type.
            await SeedContributorsAsync(DemoSeedPhase.Activity, seedContext, operations, cancellationToken);
            ClearTrackingIfSupported(_dbContext);
            ClearContributorTracking();

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

    public async Task<DemoSeedResult> ReverseAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Tenants.Write", cancellationToken);

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

            var crossBorderSetting = await GetSettingAsync(tenantId, CrossBorderDemoSeedKey, cancellationToken);
            var billCollectionSetting = await GetSettingAsync(tenantId, DemoSeedKey, cancellationToken);
            var seedType = crossBorderSetting != null
                ? DemoSeedTypes.CrossBorderPayments
                : DemoSeedTypes.BillCollection;

            var operations = new List<string>();

            await ReverseAgentActivityAsync(tenantId, operations, cancellationToken);
            await ReverseNotificationsAsync(tenantId, operations, cancellationToken);
            await ReverseOrdersAsync(tenantId, operations, cancellationToken);
            await ReverseHouseholdsAsync(tenantId, operations, cancellationToken);
            await ReverseWorkflowRegistryAsync(tenantId, operations, cancellationToken);
            await ReverseCatalogAndPricingAsync(tenantId, operations, cancellationToken);
            await ReversePartnerNetworkAsync(tenantId, operations, cancellationToken);
            await ReversePartiesAsync(tenantId, operations, cancellationToken);
            await ReverseTenantCoverageAsync(tenantId, operations, cancellationToken);
            await RestoreTenantProfileAsync(tenantId, crossBorderSetting, operations, cancellationToken);
            await RemoveSeedMarkersAsync(tenantId, billCollectionSetting, crossBorderSetting, operations, cancellationToken);

            var now = _clock.UtcNow;
            var userId = _currentUserProvider.GetCurrentUserId();

            await _auditLogWriter.LogAsync(
                AuditEventNames.TenantDemoReversed,
                "TenantDemoSeed",
                tenantId,
                tenantId,
                userId,
                _correlationContext.CorrelationId,
                JsonSerializer.Serialize(new { tenantId, seedType, operations }),
                cancellationToken);

            return new DemoSeedResult(tenantId, seedType, now, operations);
        }
        finally
        {
            tenantSeedLock.Release();
        }
    }

    // ── Contributor helpers ──────────────────────────────────────────

    private async Task SeedContributorsAsync(DemoSeedPhase phase, DemoSeedContext context, List<string> operations, CancellationToken cancellationToken)
    {
        foreach (var contributor in _contributors)
        {
            var ops = await contributor.SeedAsync(phase, context, cancellationToken);
            operations.AddRange(ops);
        }
    }

    private void ClearContributorTracking()
    {
        foreach (var contributor in _contributors)
            contributor.ClearTracking();
    }

    private IReadOnlyDictionary<string, object> GetFinanceResults()
        => _contributors.FirstOrDefault(c => c.ModuleName == "Finance")?.GetResults()
            ?? new Dictionary<string, object>();

    private IReadOnlyDictionary<string, object> GetContributorResults(string moduleName)
        => _contributors.FirstOrDefault(c => c.ModuleName == moduleName)?.GetResults()
            ?? new Dictionary<string, object>();

    private static Guid GetGuid(IReadOnlyDictionary<string, object> results, string key)
        => results.TryGetValue(key, out var value) ? (Guid)value : Guid.Empty;

    private static IReadOnlyList<Guid> GetGuidList(IReadOnlyDictionary<string, object> results, string key)
        => results.TryGetValue(key, out var value) ? (IReadOnlyList<Guid>)value : Array.Empty<Guid>();

    private static IReadOnlyDictionary<string, Guid> GetGuidDictionary(IReadOnlyDictionary<string, object> results, string key)
        => results.TryGetValue(key, out var value) ? (IReadOnlyDictionary<string, Guid>)value : new Dictionary<string, Guid>();

    private static object? GetObject(IReadOnlyDictionary<string, object> results, string key)
        => results.TryGetValue(key, out var value) ? value : null;

    private async Task<Setting?> GetSettingAsync(Guid tenantId, string key, CancellationToken cancellationToken)
    {
        return await _dbContext.Settings
            .FirstOrDefaultAsync(item => !item.IsDeleted
                                         && item.Scope == SettingScope.Tenant
                                         && item.TenantId == tenantId
                                         && item.Key == key,
                cancellationToken);
    }

    // ── Platform-only methods (unchanged) ────────────────────────────

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
            payerParty = new PartyEntity
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
            receiverParty = new PartyEntity
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

    private Task UpsertPartyContactsAsync(PartyEntity party, DateTime now, string email = "kwame.mensah@mailinator.com", string phone = "+234800000000")
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

                _dbContext.PartyContacts.Add(existingEmail);
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
                var newPhone = new PartyContact
                {
                    PartyId = party.Id,
                    Type = "Phone",
                    Value = normalizedPhone,
                    IsPrimary = false,
                    CreatedAt = now
                };
                _dbContext.PartyContacts.Add(newPhone);
                party.Contacts.Add(newPhone);
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

    private async Task UpsertMarkerAsync(
        Guid tenantId,
        (Guid PayerPartyId, Guid ReceiverPartyId, Guid RelationshipId) partyIds,
        List<string> operations,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var userId = _currentUserProvider.GetCurrentUserId();
        var financeResults = GetFinanceResults();
        var agentsResults = GetContributorResults("Agents");
        var platformResults = GetContributorResults("Platform");

        var payload = new
        {
            TenantId = tenantId,
            SeedType = DemoSeedTypes.BillCollection,
            UtilitiesCategoryId = GetGuid(financeResults, DemoSeedResultKeys.UtilitiesCategoryId),
            EcgBillerId = GetGuid(financeResults, DemoSeedResultKeys.EcgBillerId),
            WaterBillerId = GetGuid(financeResults, DemoSeedResultKeys.WaterBillerId),
            EcgServiceId = GetGuid(financeResults, DemoSeedResultKeys.EcgServiceId),
            WaterServiceId = GetGuid(financeResults, DemoSeedResultKeys.WaterServiceId),
            partyIds.PayerPartyId,
            partyIds.ReceiverPartyId,
            partyIds.RelationshipId,
            FxQuoteId = GetGuid(financeResults, DemoSeedResultKeys.FxQuoteId),
            FeePolicyId = GetGuid(financeResults, DemoSeedResultKeys.FeePolicyId),
            LimitsPolicyId = GetGuid(financeResults, DemoSeedResultKeys.LimitsPolicyId),
            OrderIds = GetGuidList(financeResults, DemoSeedResultKeys.OrderIds),
            AgentIdsByName = GetObject(agentsResults, DemoSeedResultKeys.AgentIdsByName),
            WorkflowIdsBySlug = GetObject(agentsResults, DemoSeedResultKeys.WorkflowIdsBySlug),
            AgentRunIds = GetObject(agentsResults, DemoSeedResultKeys.AgentRunIds),
            ProposalIds = GetObject(agentsResults, DemoSeedResultKeys.ProposalIds),
            NotificationIds = GetObject(platformResults, DemoSeedResultKeys.NotificationIds)
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

        tenant.SupportedCountriesJson = TenantCountryCodeSerializer.Serialize(supportedCountries);
        tenant.AllowedOriginCountriesJson = TenantCountryCodeSerializer.Serialize(supportedCountries);
        tenant.AllowedDestinationCountriesJson = TenantCountryCodeSerializer.Serialize(supportedCountries);
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

    private static void ClearTrackingIfSupported(PlatformDbContext dbContext)
    {
        dbContext.ChangeTracker.Clear();
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
            party = new PartyEntity
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
            party = new PartyEntity
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
        PartyEntity party,
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
            var newAddress = new PartyAddress
            {
                PartyId = party.Id,
                Type = type,
                Line1 = line1,
                City = city,
                State = state,
                Postcode = postcode,
                Country = country,
                CreatedAt = now
            };
            _dbContext.PartyAddresses.Add(newAddress);
            party.Addresses.Add(newAddress);
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

    private async Task UpsertCrossBorderMarkerAsync(
        Guid tenantId,
        string seedType,
        TenantSnapshot tenantSnapshot,
        (Guid PayerPartyId, Guid ReceiverPartyId, Guid RelationshipId) billCollectionParties,
        (IReadOnlyList<Guid> CountryIds, IReadOnlyList<Guid> CurrencyIds) tenantCoverage,
        (IReadOnlyList<Guid> PartyIds, IReadOnlyList<Guid> RelationshipIds) crossBorderParties,
        List<string> operations,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var userId = _currentUserProvider.GetCurrentUserId();
        var payload = new
        {
            TenantId = tenantId,
            SeedType = seedType,
            TenantSnapshot = tenantSnapshot,
            BillCollection = new
            {
                billCollectionParties.PayerPartyId,
                billCollectionParties.ReceiverPartyId,
                billCollectionParties.RelationshipId
            },
            CrossBorder = new
            {
                CountryIds = tenantCoverage.CountryIds,
                CurrencyIds = tenantCoverage.CurrencyIds,
                PartyIds = crossBorderParties.PartyIds,
                RelationshipIds = crossBorderParties.RelationshipIds,
                billCollectionParties.PayerPartyId,
                billCollectionParties.ReceiverPartyId,
                billCollectionParties.RelationshipId
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

    private async Task<TenantSnapshot> CaptureTenantSnapshotAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var tenant = await _dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == tenantId, cancellationToken)
            ?? throw new InvalidOperationException($"Tenant {tenantId} not found.");

        return new TenantSnapshot(
            tenant.Country,
            tenant.DefaultCurrency,
            tenant.City,
            tenant.StateProvince,
            tenant.AddressLine1,
            tenant.SupportedCountriesJson,
            tenant.AllowedOriginCountriesJson,
            tenant.AllowedDestinationCountriesJson);
    }

    private sealed record TenantSnapshot(
        string? Country,
        string? DefaultCurrency,
        string? City,
        string? StateProvince,
        string? AddressLine1,
        string? SupportedCountriesJson,
        string? AllowedOriginCountriesJson,
        string? AllowedDestinationCountriesJson);

    private async Task ReverseAgentActivityAsync(Guid tenantId, List<string> operations, CancellationToken cancellationToken)
    {
        var agentIds = await _agentsDbContext.Agents
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && DemoAgentNames.Contains(item.Name))
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);

        if (agentIds.Count == 0)
        {
            return;
        }

        var proposalCount = await _agentsDbContext.Proposals
            .IgnoreQueryFilters()
            .Where(item => item.TenantId == tenantId && agentIds.Contains(item.ProposedByAgentId))
            .ExecuteDeleteAsync(cancellationToken);

        var runCount = await _agentsDbContext.AgentRuns
            .IgnoreQueryFilters()
            .Where(item => item.TenantId == tenantId && agentIds.Contains(item.AgentId))
            .ExecuteDeleteAsync(cancellationToken);

        if (proposalCount > 0 || runCount > 0)
        {
            operations.Add($"Removed {proposalCount} proposals and {runCount} agent runs");
        }
    }

    private async Task ReverseNotificationsAsync(Guid tenantId, List<string> operations, CancellationToken cancellationToken)
    {
        var count = await _dbContext.Notifications
            .IgnoreQueryFilters()
            .Where(item => item.TenantId == tenantId
                           && DemoNotificationTypes.Contains(item.Type))
            .ExecuteDeleteAsync(cancellationToken);

        if (count > 0)
        {
            operations.Add($"Removed {count} demo notifications");
        }
    }

    private async Task ReverseOrdersAsync(Guid tenantId, List<string> operations, CancellationToken cancellationToken)
    {
        var orderIds = await _financeDbContext.Orders
            .IgnoreQueryFilters()
            .Where(item => item.TenantId == tenantId && item.ProvenanceJson.Contains("demo-seed"))
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);

        if (orderIds.Count == 0)
        {
            return;
        }

        await _financeDbContext.OrderPartyRoles
            .IgnoreQueryFilters()
            .Where(item => orderIds.Contains(item.OrderId))
            .ExecuteDeleteAsync(cancellationToken);

        await _financeDbContext.OrderItems
            .IgnoreQueryFilters()
            .Where(item => orderIds.Contains(item.OrderId))
            .ExecuteDeleteAsync(cancellationToken);

        var orderCount = await _financeDbContext.Orders
            .IgnoreQueryFilters()
            .Where(item => orderIds.Contains(item.Id))
            .ExecuteDeleteAsync(cancellationToken);

        operations.Add($"Removed {orderCount} demo orders");
    }

    private async Task ReverseHouseholdsAsync(Guid tenantId, List<string> operations, CancellationToken cancellationToken)
    {
        var householdIds = await _financeDbContext.Households
            .IgnoreQueryFilters()
            .Where(item => item.TenantId == tenantId && DemoHouseholdNames.Contains(item.Name))
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);

        if (householdIds.Count == 0)
        {
            return;
        }

        await _financeDbContext.HouseholdMembers
            .IgnoreQueryFilters()
            .Where(item => householdIds.Contains(item.HouseholdId))
            .ExecuteDeleteAsync(cancellationToken);

        var householdCount = await _financeDbContext.Households
            .IgnoreQueryFilters()
            .Where(item => householdIds.Contains(item.Id))
            .ExecuteDeleteAsync(cancellationToken);

        operations.Add($"Removed {householdCount} demo households");
    }

    private async Task ReverseWorkflowRegistryAsync(Guid tenantId, List<string> operations, CancellationToken cancellationToken)
    {
        var workflowIds = await _agentsDbContext.Workflows
            .IgnoreQueryFilters()
            .Where(item => item.TenantId == tenantId && DemoWorkflowSlugs.Contains(item.Slug))
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);

        if (workflowIds.Count > 0)
        {
            await _agentsDbContext.WorkflowRuns
                .IgnoreQueryFilters()
                .Where(item => workflowIds.Contains(item.WorkflowId))
                .ExecuteDeleteAsync(cancellationToken);

            await _agentsDbContext.WorkflowVersions
                .IgnoreQueryFilters()
                .Where(item => workflowIds.Contains(item.WorkflowId))
                .ExecuteDeleteAsync(cancellationToken);

            await _agentsDbContext.WorkflowComments
                .IgnoreQueryFilters()
                .Where(item => workflowIds.Contains(item.WorkflowId))
                .ExecuteDeleteAsync(cancellationToken);

            await _agentsDbContext.WorkflowEdges
                .IgnoreQueryFilters()
                .Where(item => workflowIds.Contains(item.WorkflowId))
                .ExecuteDeleteAsync(cancellationToken);

            await _agentsDbContext.WorkflowNodes
                .IgnoreQueryFilters()
                .Where(item => workflowIds.Contains(item.WorkflowId))
                .ExecuteDeleteAsync(cancellationToken);

            var workflowCount = await _agentsDbContext.Workflows
                .IgnoreQueryFilters()
                .Where(item => workflowIds.Contains(item.Id))
                .ExecuteDeleteAsync(cancellationToken);

            operations.Add($"Removed {workflowCount} demo workflows");
        }

        var agentCount = await _agentsDbContext.Agents
            .IgnoreQueryFilters()
            .Where(item => item.TenantId == tenantId && DemoAgentNames.Contains(item.Name))
            .ExecuteDeleteAsync(cancellationToken);

        if (agentCount > 0)
        {
            operations.Add($"Removed {agentCount} demo agents");
        }
    }

    private async Task ReversePartnerNetworkAsync(Guid tenantId, List<string> operations, CancellationToken cancellationToken)
    {
        var partnerIds = await _financeDbContext.Partners
            .IgnoreQueryFilters()
            .Where(item => item.TenantId == tenantId && DemoPartnerNames.Contains(item.Name))
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);

        if (partnerIds.Count == 0)
        {
            return;
        }

        var partnerFundingAccountIds = await _financeDbContext.PartnerFundingAccounts
            .IgnoreQueryFilters()
            .Where(item => item.TenantId == tenantId && partnerIds.Contains(item.PartnerId))
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);

        var ledgerAccountIds = await _financeDbContext.PartnerFundingAccounts
            .IgnoreQueryFilters()
            .Where(item => item.TenantId == tenantId && partnerIds.Contains(item.PartnerId))
            .Select(item => item.LedgerAccountId)
            .ToListAsync(cancellationToken);

        if (partnerFundingAccountIds.Count > 0)
        {
            var journalEntryIds = await _financeDbContext.JournalEntries
                .IgnoreQueryFilters()
                .Where(item => item.TenantId == tenantId && partnerFundingAccountIds.Contains(item.SourceId))
                .Select(item => item.Id)
                .ToListAsync(cancellationToken);

            if (journalEntryIds.Count > 0)
            {
                await _financeDbContext.JournalEntryLines
                    .IgnoreQueryFilters()
                    .Where(item => journalEntryIds.Contains(item.JournalEntryId))
                    .ExecuteDeleteAsync(cancellationToken);

                await _financeDbContext.JournalEntries
                    .IgnoreQueryFilters()
                    .Where(item => journalEntryIds.Contains(item.Id))
                    .ExecuteDeleteAsync(cancellationToken);
            }

            await _financeDbContext.PartnerFundingAccounts
                .IgnoreQueryFilters()
                .Where(item => partnerFundingAccountIds.Contains(item.Id))
                .ExecuteDeleteAsync(cancellationToken);
        }

        if (ledgerAccountIds.Count > 0)
        {
            await _financeDbContext.LedgerAccounts
                .IgnoreQueryFilters()
                .Where(item => ledgerAccountIds.Contains(item.Id))
                .ExecuteDeleteAsync(cancellationToken);
        }

        await _financeDbContext.RoutingRules
            .IgnoreQueryFilters()
            .Where(item => item.TenantId == tenantId && item.TargetPartnerId.HasValue && partnerIds.Contains(item.TargetPartnerId.Value))
            .ExecuteDeleteAsync(cancellationToken);

        await _financeDbContext.Connectors
            .IgnoreQueryFilters()
            .Where(item => item.TenantId == tenantId && partnerIds.Contains(item.PartnerId))
            .ExecuteDeleteAsync(cancellationToken);

        await _financeDbContext.PartnerBranches
            .IgnoreQueryFilters()
            .Where(item => item.TenantId == tenantId && partnerIds.Contains(item.PartnerId))
            .ExecuteDeleteAsync(cancellationToken);

        var partnerCount = await _financeDbContext.Partners
            .IgnoreQueryFilters()
            .Where(item => partnerIds.Contains(item.Id))
            .ExecuteDeleteAsync(cancellationToken);

        operations.Add($"Removed {partnerCount} demo partners and routing configuration");
    }

    private async Task ReverseCatalogAndPricingAsync(Guid tenantId, List<string> operations, CancellationToken cancellationToken)
    {
        await _financeDbContext.CatalogBillerServices
            .IgnoreQueryFilters()
            .Where(item => item.TenantId == tenantId && item.ServiceCode.StartsWith("BILLPAY."))
            .ExecuteDeleteAsync(cancellationToken);

        await _financeDbContext.CatalogBillers
            .IgnoreQueryFilters()
            .Where(item => item.TenantId == tenantId)
            .ExecuteDeleteAsync(cancellationToken);

        var categoryCount = await _financeDbContext.CatalogBillerCategories
            .IgnoreQueryFilters()
            .Where(item => item.TenantId == tenantId && item.Name == "Utilities")
            .ExecuteDeleteAsync(cancellationToken);

        await _financeDbContext.FxQuotes
            .IgnoreQueryFilters()
            .Where(item => item.TenantId == tenantId && item.Provider == "DemoRate")
            .ExecuteDeleteAsync(cancellationToken);

        await _financeDbContext.FeePolicies
            .IgnoreQueryFilters()
            .Where(item => item.TenantId == tenantId && (item.Name == "BillPay-NG-GH-Default" || item.Name.StartsWith("CrossBorder-")))
            .ExecuteDeleteAsync(cancellationToken);

        await _financeDbContext.LimitsPolicies
            .IgnoreQueryFilters()
            .Where(item => item.TenantId == tenantId && item.ScopeType == "Tenant" && item.ScopeId == tenantId)
            .ExecuteDeleteAsync(cancellationToken);

        if (categoryCount > 0)
        {
            operations.Add($"Removed {categoryCount} demo catalog categories and demo pricing");
        }
    }

    private async Task ReversePartiesAsync(Guid tenantId, List<string> operations, CancellationToken cancellationToken)
    {
        var partyIds = new[]
        {
            DemoPayerPartyId, DemoReceiverPartyId, TundePartyId, AdwoaPartyId, PeterPartyId,
            NalediPartyId, AishaPartyId, KofiPartyId, AcmeImportsPartyId, SafariFreightPartyId,
            OliviaPartyId, LiamPartyId
        };

        await _dbContext.PartyRelationships
            .IgnoreQueryFilters()
            .Where(item => item.TenantId == tenantId && (partyIds.Contains(item.FromPartyId) || partyIds.Contains(item.ToPartyId)))
            .ExecuteDeleteAsync(cancellationToken);

        await _dbContext.PartyRoleAssignments
            .IgnoreQueryFilters()
            .Where(item => item.TenantId == tenantId && partyIds.Contains(item.PartyId))
            .ExecuteDeleteAsync(cancellationToken);

        await _dbContext.BusinessProfiles
            .IgnoreQueryFilters()
            .Where(item => partyIds.Contains(item.PartyId))
            .ExecuteDeleteAsync(cancellationToken);

        await _dbContext.PersonProfiles
            .IgnoreQueryFilters()
            .Where(item => partyIds.Contains(item.PartyId))
            .ExecuteDeleteAsync(cancellationToken);

        await _dbContext.PartyAddresses
            .IgnoreQueryFilters()
            .Where(item => partyIds.Contains(item.PartyId))
            .ExecuteDeleteAsync(cancellationToken);

        await _dbContext.PartyContacts
            .IgnoreQueryFilters()
            .Where(item => partyIds.Contains(item.PartyId))
            .ExecuteDeleteAsync(cancellationToken);

        var partyCount = await _dbContext.Parties
            .IgnoreQueryFilters()
            .Where(item => item.TenantId == tenantId && partyIds.Contains(item.Id))
            .ExecuteDeleteAsync(cancellationToken);

        if (partyCount > 0)
        {
            operations.Add($"Removed {partyCount} demo parties and relationships");
        }
    }

    private async Task ReverseTenantCoverageAsync(Guid tenantId, List<string> operations, CancellationToken cancellationToken)
    {
        using var crossBorderDocument = await TryParseSettingDocumentAsync(tenantId, CrossBorderDemoSeedKey, cancellationToken);
        var countryIds = ReadGuidArray(crossBorderDocument, "CrossBorder", "CountryIds");
        var currencyIds = ReadGuidArray(crossBorderDocument, "CrossBorder", "CurrencyIds");

        if (countryIds.Count == 0 && currencyIds.Count == 0)
        {
            return;
        }

        var countryCount = await _dbContext.TenantCountries
            .IgnoreQueryFilters()
            .Where(item => item.TenantId == tenantId && countryIds.Contains(item.CountryId))
            .ExecuteDeleteAsync(cancellationToken);

        var currencyCount = await _dbContext.TenantCurrencies
            .IgnoreQueryFilters()
            .Where(item => item.TenantId == tenantId && currencyIds.Contains(item.CurrencyId))
            .ExecuteDeleteAsync(cancellationToken);

        if (countryCount > 0 || currencyCount > 0)
        {
            operations.Add($"Removed {countryCount} tenant countries and {currencyCount} tenant currencies added for demo coverage");
        }
    }

    private async Task RestoreTenantProfileAsync(Guid tenantId, Setting? crossBorderSetting, List<string> operations, CancellationToken cancellationToken)
    {
        if (crossBorderSetting == null || string.IsNullOrWhiteSpace(crossBorderSetting.Value))
        {
            return;
        }

        using var document = JsonDocument.Parse(crossBorderSetting.Value);
        if (!document.RootElement.TryGetProperty("TenantSnapshot", out var snapshotElement))
        {
            return;
        }

        var snapshot = JsonSerializer.Deserialize<TenantSnapshot>(snapshotElement.GetRawText());
        if (snapshot == null)
        {
            return;
        }

        var tenant = await _dbContext.Tenants.FirstOrDefaultAsync(item => item.Id == tenantId, cancellationToken);
        if (tenant == null)
        {
            throw new InvalidOperationException($"Tenant {tenantId} not found.");
        }

        tenant.Country = snapshot.Country ?? string.Empty;
        tenant.DefaultCurrency = snapshot.DefaultCurrency ?? string.Empty;
        tenant.City = snapshot.City;
        tenant.StateProvince = snapshot.StateProvince;
        tenant.AddressLine1 = snapshot.AddressLine1;
        tenant.SupportedCountriesJson = snapshot.SupportedCountriesJson;
        tenant.AllowedOriginCountriesJson = snapshot.AllowedOriginCountriesJson;
        tenant.AllowedDestinationCountriesJson = snapshot.AllowedDestinationCountriesJson;
        tenant.UpdatedAt = _clock.UtcNow;
        tenant.UpdatedBy = _currentUserProvider.GetCurrentUserId();

        await _dbContext.SaveChangesAsync(cancellationToken);
        operations.Add("Restored tenant home-base settings from pre-demo snapshot");
    }

    private async Task<JsonDocument?> TryParseSettingDocumentAsync(Guid tenantId, string key, CancellationToken cancellationToken)
    {
        var setting = await GetSettingAsync(tenantId, key, cancellationToken);
        if (setting == null || string.IsNullOrWhiteSpace(setting.Value))
        {
            return null;
        }

        return JsonDocument.Parse(setting.Value);
    }

    private static IReadOnlyList<Guid> ReadGuidArray(JsonDocument? document, string sectionName, string propertyName)
    {
        if (document == null)
        {
            return Array.Empty<Guid>();
        }

        if (!document.RootElement.TryGetProperty(sectionName, out var section))
        {
            return Array.Empty<Guid>();
        }

        if (!section.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<Guid>();
        }

        var ids = new List<Guid>();
        foreach (var item in property.EnumerateArray())
        {
            if (item.TryGetGuid(out var id))
            {
                ids.Add(id);
            }
        }

        return ids;
    }

    private async Task RemoveSeedMarkersAsync(Guid tenantId, Setting? billCollectionSetting, Setting? crossBorderSetting, List<string> operations, CancellationToken cancellationToken)
    {
        if (billCollectionSetting != null || crossBorderSetting != null)
        {
            await _dbContext.Settings
                .IgnoreQueryFilters()
                .Where(item => !item.IsDeleted
                               && item.Scope == SettingScope.Tenant
                               && item.TenantId == tenantId
                               && (item.Key == DemoSeedKey || item.Key == CrossBorderDemoSeedKey))
                .ExecuteDeleteAsync(cancellationToken);

            operations.Add("Removed demo seed markers");
        }
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
            throw new PermissionDeniedException(permissionKey);
        }
    }
}
