using System.Text.Json;
using Microsoft.EntityFrameworkCore;

using Aonik.Platform.Contracts.Services.Customers;
using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using PartyEntity = Aonik.Platform.Entities.Party.Party;

namespace Aonik.Platform.Services.Customers;

internal class CustomerDataService : AdminServiceBase, ICustomerDataService
{
    private const string BundleVersion = "1.0";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly PlatformDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IClock _clock;
    private readonly ICustomerDataExportProvider _exportProvider;
    private readonly ICustomerDataImportConsumer _importConsumer;

    public CustomerDataService(
        PlatformDbContext dbContext,
        ITenantProvider tenantProvider,
        IClock clock,
        ICurrentUserProvider currentUserProvider,
        IPermissionService permissionService,
        ICustomerDataExportProvider exportProvider,
        ICustomerDataImportConsumer importConsumer)
        : base(currentUserProvider, permissionService)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _clock = clock;
        _exportProvider = exportProvider;
        _importConsumer = importConsumer;
    }

    // ─── Export ────────────────────────────────────────────────────

    public async Task<CustomerDataBundle?> ExportAsync(
        Guid partyId,
        CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Customers.Read", cancellationToken);

        var tenantId = _tenantProvider.GetCurrentTenantId();

        // 1. Load the root party
        var party = await _dbContext.Parties
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == partyId && p.TenantId == tenantId, cancellationToken);

        if (party == null)
            return null;

        var data = new Dictionary<string, List<JsonElement>>();

        // 2. Export Platform entities
        data["Party"] = Serialize(new[] { party });

        var personProfiles = await _dbContext.PersonProfiles
            .AsNoTracking()
            .Where(p => p.PartyId == partyId)
            .ToListAsync(cancellationToken);
        if (personProfiles.Count > 0)
            data["PersonProfile"] = Serialize(personProfiles);

        var businessProfiles = await _dbContext.BusinessProfiles
            .AsNoTracking()
            .Where(p => p.PartyId == partyId)
            .ToListAsync(cancellationToken);
        if (businessProfiles.Count > 0)
            data["BusinessProfile"] = Serialize(businessProfiles);

        var addresses = await _dbContext.PartyAddresses
            .AsNoTracking()
            .Where(a => a.PartyId == partyId)
            .ToListAsync(cancellationToken);
        if (addresses.Count > 0)
            data["PartyAddress"] = Serialize(addresses);

        var contacts = await _dbContext.PartyContacts
            .AsNoTracking()
            .Where(c => c.PartyId == partyId)
            .ToListAsync(cancellationToken);
        if (contacts.Count > 0)
            data["PartyContact"] = Serialize(contacts);

        var consents = await _dbContext.PartyConsents
            .AsNoTracking()
            .Where(c => c.PartyId == partyId)
            .ToListAsync(cancellationToken);
        if (consents.Count > 0)
            data["PartyConsent"] = Serialize(consents);

        var partyAccounts = await _dbContext.PartyAccounts
            .AsNoTracking()
            .Where(a => a.PartyId == partyId && a.TenantId == tenantId)
            .ToListAsync(cancellationToken);
        // Redact provider references
        foreach (var pa in partyAccounts)
            pa.ProviderRef = null;
        if (partyAccounts.Count > 0)
            data["PartyAccount"] = Serialize(partyAccounts);

        var roleAssignments = await _dbContext.PartyRoleAssignments
            .AsNoTracking()
            .Where(r => r.PartyId == partyId && r.TenantId == tenantId)
            .ToListAsync(cancellationToken);
        if (roleAssignments.Count > 0)
            data["PartyRoleAssignment"] = Serialize(roleAssignments);

        var relationships = await _dbContext.PartyRelationships
            .AsNoTracking()
            .Where(r => (r.FromPartyId == partyId || r.ToPartyId == partyId) && r.TenantId == tenantId)
            .ToListAsync(cancellationToken);
        if (relationships.Count > 0)
            data["PartyRelationship"] = Serialize(relationships);

        var notificationPrefs = await _dbContext.NotificationPreferences
            .AsNoTracking()
            .Where(n => n.PartyId == partyId)
            .ToListAsync(cancellationToken);
        if (notificationPrefs.Count > 0)
            data["NotificationPreference"] = Serialize(notificationPrefs);

        var marketingPrefs = await _dbContext.MarketingPreferences
            .AsNoTracking()
            .Where(m => m.PartyId == partyId)
            .ToListAsync(cancellationToken);
        if (marketingPrefs.Count > 0)
            data["MarketingPreference"] = Serialize(marketingPrefs);

        // 3. Resolve linked Users
        var userParties = await _dbContext.UserParties
            .AsNoTracking()
            .Where(up => up.PartyId == partyId && up.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        var userIds = userParties.Select(up => up.UserId).Distinct().ToList();

        if (userIds.Count > 0)
        {
            var users = await _dbContext.Users
                .AsNoTracking()
                .Where(u => userIds.Contains(u.Id) && u.TenantId == tenantId)
                .ToListAsync(cancellationToken);

            // Redact IdP references — environment-specific
            foreach (var user in users)
            {
                user.ExternalSubject = null;
                user.ExternalIssuer = null;
                user.ExternalTenantId = null;
            }

            if (users.Count > 0)
                data["User"] = Serialize(users);
        }

        if (userParties.Count > 0)
            data["UserParty"] = Serialize(userParties);

        // 4. Export Finance module entities via cross-module contract
        var financeData = await _exportProvider.ExportAsync(tenantId, userIds, cancellationToken);
        foreach (var (entityType, entities) in financeData)
        {
            data[entityType] = entities;
        }

        // 5. Build entity counts
        var entityCounts = data.ToDictionary(kv => kv.Key, kv => kv.Value.Count);

        return new CustomerDataBundle
        {
            Version = BundleVersion,
            ExportedAt = _clock.UtcNow,
            SourceEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
            SourceTenantId = tenantId,
            RootPartyId = partyId,
            EntityCounts = entityCounts,
            Data = data,
        };
    }

    // ─── Import ────────────────────────────────────────────────────

    public async Task<CustomerDataImportResult> ImportAsync(
        CustomerDataBundle bundle,
        string conflictMode = "fail",
        CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Customers.Write", cancellationToken);

        var tenantId = _tenantProvider.GetCurrentTenantId();
        var warnings = new List<string>();
        var idMap = new Dictionary<Guid, Guid>();

        // 1. Pre-scan all entities to build the ID remap dictionary
        foreach (var (_, entities) in bundle.Data)
        {
            foreach (var entity in entities)
            {
                if (entity.TryGetProperty("id", out var idProp) &&
                    idProp.TryGetGuid(out var oldId) &&
                    !idMap.ContainsKey(oldId))
                {
                    idMap[oldId] = Guid.NewGuid();
                }
            }
        }

        // 2. Import Platform entities
        var platformCounts = new Dictionary<string, int>();

        // Party
        var newPartyId = Guid.Empty;
        if (bundle.Data.TryGetValue("Party", out var partyList) && partyList.Count > 0)
        {
            foreach (var partyJson in partyList)
            {
                var party = Deserialize<PartyEntity>(partyJson);
                if (party == null) continue;

                var oldId = party.Id;
                party.Id = idMap.GetValueOrDefault(oldId, Guid.NewGuid());
                party.TenantId = tenantId;
                newPartyId = party.Id;
                ResetAuditFields(party);

                // Conflict check: does a party with the same display name already exist?
                if (conflictMode == "fail")
                {
                    var exists = await _dbContext.Parties
                        .AnyAsync(p => p.TenantId == tenantId && p.DisplayName == party.DisplayName, cancellationToken);
                    if (exists)
                        throw new InvalidOperationException(
                            $"A customer with display name '{party.DisplayName}' already exists in this tenant. Use conflictMode=skip to skip conflicts.");
                }

                _dbContext.Parties.Add(party);
            }
            platformCounts["Party"] = partyList.Count;
        }

        // Generic Platform entity import helper
        await ImportEntities<Entities.Party.PersonProfile>("PersonProfile", bundle.Data, idMap, tenantId, platformCounts, cancellationToken);
        await ImportEntities<Entities.Party.BusinessProfile>("BusinessProfile", bundle.Data, idMap, tenantId, platformCounts, cancellationToken);
        await ImportEntities<Entities.Party.PartyAddress>("PartyAddress", bundle.Data, idMap, tenantId, platformCounts, cancellationToken);
        await ImportEntities<Entities.Party.PartyContact>("PartyContact", bundle.Data, idMap, tenantId, platformCounts, cancellationToken);
        await ImportEntities<Entities.Party.PartyConsent>("PartyConsent", bundle.Data, idMap, tenantId, platformCounts, cancellationToken);
        await ImportEntities<Entities.Party.PartyAccount>("PartyAccount", bundle.Data, idMap, tenantId, platformCounts, cancellationToken);
        await ImportEntities<Entities.Party.PartyRoleAssignment>("PartyRoleAssignment", bundle.Data, idMap, tenantId, platformCounts, cancellationToken);
        await ImportEntities<Entities.Party.PartyRelationship>("PartyRelationship", bundle.Data, idMap, tenantId, platformCounts, cancellationToken);
        await ImportEntities<Entities.Party.NotificationPreference>("NotificationPreference", bundle.Data, idMap, tenantId, platformCounts, cancellationToken);
        await ImportEntities<Entities.Party.MarketingPreference>("MarketingPreference", bundle.Data, idMap, tenantId, platformCounts, cancellationToken);
        await ImportEntities<Entities.Identity.User>("User", bundle.Data, idMap, tenantId, platformCounts, cancellationToken);
        await ImportEntities<Entities.Identity.UserParty>("UserParty", bundle.Data, idMap, tenantId, platformCounts, cancellationToken);

        // Save Platform entities first (they are FK parents for Finance entities)
        await _dbContext.SaveChangesAsync(cancellationToken);

        // 3. Import Finance module entities via cross-module contract
        var financeResult = await _importConsumer.ImportAsync(tenantId, bundle.Data, idMap, cancellationToken);

        // 4. Merge results
        var allCounts = new Dictionary<string, int>(platformCounts);
        foreach (var (key, count) in financeResult.EntityCounts)
            allCounts[key] = count;

        warnings.AddRange(financeResult.Warnings);

        return new CustomerDataImportResult
        {
            NewPartyId = newPartyId,
            EntityCounts = allCounts,
            IdMap = idMap,
            Warnings = warnings,
        };
    }

    // ─── Helpers ───────────────────────────────────────────────────

    private static List<JsonElement> Serialize<T>(IEnumerable<T> entities)
    {
        return entities
            .Select(e => JsonSerializer.SerializeToElement(e, JsonOptions))
            .ToList();
    }

    private T? Deserialize<T>(JsonElement element) where T : class
    {
        return JsonSerializer.Deserialize<T>(element.GetRawText(), JsonOptions);
    }

    private async Task ImportEntities<T>(
        string entityTypeName,
        Dictionary<string, List<JsonElement>> data,
        Dictionary<Guid, Guid> idMap,
        Guid tenantId,
        Dictionary<string, int> counts,
        CancellationToken cancellationToken) where T : class
    {
        if (!data.TryGetValue(entityTypeName, out var entityList) || entityList.Count == 0)
            return;

        var count = 0;
        foreach (var json in entityList)
        {
            var entity = Deserialize<T>(json);
            if (entity == null) continue;

            RemapIds(entity, idMap);
            SetTenantId(entity, tenantId);
            ResetAuditFields(entity);

            _dbContext.Set<T>().Add(entity);
            count++;
        }

        counts[entityTypeName] = count;
        await Task.CompletedTask;
    }

    /// <summary>
    /// Walks all Guid and Guid? properties on the entity and remaps
    /// any values found in the idMap. This handles Id, PartyId, UserId,
    /// and all other FK references generically.
    /// </summary>
    private static void RemapIds<T>(T entity, Dictionary<Guid, Guid> idMap) where T : class
    {
        var type = entity.GetType();
        foreach (var prop in type.GetProperties())
        {
            if (prop.PropertyType == typeof(Guid) && prop.CanWrite)
            {
                var value = (Guid)prop.GetValue(entity)!;
                if (idMap.TryGetValue(value, out var newValue))
                    prop.SetValue(entity, newValue);
            }
            else if (prop.PropertyType == typeof(Guid?) && prop.CanWrite)
            {
                var value = (Guid?)prop.GetValue(entity);
                if (value.HasValue && idMap.TryGetValue(value.Value, out var newValue))
                    prop.SetValue(entity, newValue);
            }
        }
    }

    /// <summary>
    /// Sets TenantId on the entity if it has one (ITenantScoped).
    /// </summary>
    private static void SetTenantId<T>(T entity, Guid tenantId) where T : class
    {
        var tenantProp = entity.GetType().GetProperty("TenantId");
        if (tenantProp != null && tenantProp.CanWrite && tenantProp.PropertyType == typeof(Guid))
        {
            tenantProp.SetValue(entity, tenantId);
        }
    }

    /// <summary>
    /// Resets audit and concurrency fields so the entity appears freshly created.
    /// </summary>
    private static void ResetAuditFields<T>(T entity) where T : class
    {
        var type = entity.GetType();

        var rowVersion = type.GetProperty("RowVersion");
        if (rowVersion != null && rowVersion.CanWrite && rowVersion.PropertyType == typeof(byte[]))
            rowVersion.SetValue(entity, Array.Empty<byte>());

        var updatedAt = type.GetProperty("UpdatedAt");
        if (updatedAt != null && updatedAt.CanWrite && updatedAt.PropertyType == typeof(DateTime?))
            updatedAt.SetValue(entity, null);
    }
}
