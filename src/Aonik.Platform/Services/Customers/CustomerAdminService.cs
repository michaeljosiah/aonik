using System.Text.Json;
using Microsoft.EntityFrameworkCore;

using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Platform.Persistence;
using Aonik.Platform.Contracts.Models.Customers;
using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Services;
using Aonik.Platform.Services.Compliance;
using Aonik.Platform.Contracts.Services.Compliance;
using Aonik.Platform.Contracts.Services.Customers;
using Aonik.Platform.Contracts.Services.Identity;
using Aonik.Platform.Entities.Party;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Documents;
using Aonik.SharedKernel.Abstractions.Ordering;
using PartyEntity = Aonik.Platform.Entities.Party.Party;

namespace Aonik.Platform.Services.Customers;

internal class CustomerAdminService : AdminServiceBase, ICustomerAdminService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly PlatformDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IClock _clock;
    private readonly IAuditLogWriter _auditLogWriter;
    private readonly ICustomerFinanceStatsProvider _financeStatsProvider;
    private readonly ICustomerActivityProvider _activityProvider;
    private readonly IDocumentReader _documentReader;
    private readonly IReadOnlyList<ICustomerRegistryContributor> _registryContributors;
    private readonly IOrderService _orders;

    public CustomerAdminService(
        PlatformDbContext dbContext,
        ITenantProvider tenantProvider,
        IClock clock,
        IAuditLogWriter auditLogWriter,
        ICurrentUserProvider currentUserProvider,
        IPermissionService permissionService,
        ICustomerFinanceStatsProvider financeStatsProvider,
        ICustomerActivityProvider activityProvider,
        IDocumentReader documentReader,
        IEnumerable<ICustomerRegistryContributor> registryContributors,
        IOrderService orders)
        : base(currentUserProvider, permissionService)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _clock = clock;
        _auditLogWriter = auditLogWriter;
        _financeStatsProvider = financeStatsProvider;
        _activityProvider = activityProvider;
        _documentReader = documentReader;
        _registryContributors = registryContributors.ToList();
        _orders = orders;
    }

    /// <summary>
    /// Who is IN the registry: a tenant-scoped party holding the Customer role. Shared by the
    /// list and the domain metadata so "this domain has customers" can never mean something
    /// different from "these rows are listed".
    /// </summary>
    private IQueryable<PartyEntity> RegistryCustomerQuery(Guid tenantId) => _dbContext.Parties
        .AsNoTracking()
        .Where(party => party.TenantId == tenantId)
        .Where(party => _dbContext.PartyRoleAssignments.Any(ra =>
            ra.PartyId == party.Id &&
            ra.TenantId == tenantId &&
            ra.Role == PartyRoles.Customer &&
            ra.ContextType == "Tenant" &&
            ra.ContextId == tenantId));

    public async Task<CustomerRegistryDomainsResponse> GetRegistryDomainsAsync(
        CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Customers.Read", cancellationToken);

        var tenantId = _tenantProvider.GetCurrentTenantId();

        // A domain is "active" only if it has participants that are ALSO registry customers.
        // A module's ownership records do not all correspond to a customer — the PersonalFinance
        // seed, for instance, writes profiles with an empty PartyId — and counting them would
        // advertise a tab that selects to an empty table, because the registry query still
        // requires the tenant-scoped Customer role assignment. So the check intersects with
        // exactly the predicate ListCustomersAsync applies.
        var active = new List<string>();
        foreach (var contributor in _registryContributors)
        {
            var participants = await contributor.GetParticipantsAsync(null, cancellationToken);
            if (participants.Count == 0)
            {
                continue;
            }

            var ids = participants.ToList();
            var hasRegistryCustomer = await RegistryCustomerQuery(tenantId)
                .AnyAsync(party => ids.Contains(party.Id), cancellationToken);
            if (hasRegistryCustomer)
            {
                active.Add(contributor.DomainKey);
            }
        }

        return new CustomerRegistryDomainsResponse(
            active.Distinct(StringComparer.Ordinal).OrderBy(d => d, StringComparer.Ordinal).ToList());
    }

    public async Task<PagedResult<CustomerListItem>> ListCustomersAsync(
        ListCustomersRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Customers.Read", cancellationToken);

        var tenantId = _tenantProvider.GetCurrentTenantId();
        var pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;
        var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;

        var query = RegistryCustomerQuery(tenantId);

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim();
            query = query.Where(party => party.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(request.PartyType))
        {
            var partyType = request.PartyType.Trim();
            if (IsPersonPartyType(partyType))
            {
                query = query.Where(party => party.PartyType == "Person" || party.PartyType == "Individual");
            }
            else
            {
                query = query.Where(party => party.PartyType == partyType);
            }
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(party =>
                party.DisplayName.Contains(search) ||
                _dbContext.PartyContacts.Any(c => c.PartyId == party.Id && c.Value.Contains(search)));
        }

        // Spec 080 — the domain filter narrows the registry BEFORE the count and the page, so
        // paging stays correct; filtering a loaded page would report the wrong total and drop
        // matches on later pages. An unknown key has no contributor and therefore matches
        // nothing, which is the honest answer rather than silently ignoring the filter.
        if (!string.IsNullOrWhiteSpace(request.Domain))
        {
            var domain = request.Domain.Trim();
            var contributor = _registryContributors
                .FirstOrDefault(c => string.Equals(c.DomainKey, domain, StringComparison.OrdinalIgnoreCase));
            var participants = contributor is null
                ? new HashSet<Guid>()
                : (ISet<Guid>)(await contributor.GetParticipantsAsync(null, cancellationToken)).ToHashSet();
            query = query.Where(party => participants.Contains(party.Id));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var parties = await query
            .OrderBy(party => party.DisplayName)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(party => new
            {
                party.Id,
                party.DisplayName,
                party.PartyType,
                party.Status,
                party.CreatedAt
            })
            .ToListAsync(cancellationToken);

        if (parties.Count == 0)
        {
            return new PagedResult<CustomerListItem>(
                Items: new List<CustomerListItem>(),
                TotalCount: totalCount,
                PageNumber: pageNumber,
                PageSize: pageSize);
        }

        var partyIds = parties.Select(p => p.Id).ToList();

        var contacts = await _dbContext.PartyContacts
            .AsNoTracking()
            .Where(c => partyIds.Contains(c.PartyId) && (c.Type == "Email" || c.Type == "Phone"))
            .Select(c => new { c.PartyId, c.Type, c.Value, c.IsPrimary, c.CreatedAt })
            .ToListAsync(cancellationToken);

        var primaryContacts = contacts
            .GroupBy(c => c.PartyId)
            .ToDictionary(
                g => g.Key,
                g => new
                {
                    Email = g.Where(x => x.Type == "Email")
                        .OrderByDescending(x => x.IsPrimary)
                        .ThenBy(x => x.CreatedAt)
                        .Select(x => x.Value)
                        .FirstOrDefault(),
                    Phone = g.Where(x => x.Type == "Phone")
                        .OrderByDescending(x => x.IsPrimary)
                        .ThenBy(x => x.CreatedAt)
                        .Select(x => x.Value)
                        .FirstOrDefault()
                });

        var personProfiles = await _dbContext.PersonProfiles
            .AsNoTracking()
            .Where(pp => partyIds.Contains(pp.PartyId))
            .Select(pp => new { pp.PartyId, pp.PhotoUrlTiny, pp.IdvStatus })
            .ToListAsync(cancellationToken);

        var businessProfiles = await _dbContext.BusinessProfiles
            .AsNoTracking()
            .Where(bp => partyIds.Contains(bp.PartyId))
            .Select(bp => new { bp.PartyId, bp.KybStatus })
            .ToListAsync(cancellationToken);

        // Country is the party's own recorded fact — its earliest address. PartyAddress has no
        // primary flag, only a free-text Type, so ranking one type over another would invent a
        // precedence the model does not define; earliest-then-id is deterministic instead. Never
        // inferred from an order corridor or a profile, which describe something else.
        var addresses = await _dbContext.PartyAddresses
            .AsNoTracking()
            .Where(a => partyIds.Contains(a.PartyId) && a.Country != "")
            .Select(a => new { a.PartyId, a.Country, a.CreatedAt, a.Id })
            .ToListAsync(cancellationToken);
        var countryByPartyId = addresses
            .GroupBy(a => a.PartyId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(a => a.CreatedAt).ThenBy(a => a.Id).Select(a => a.Country).First());

        // Domain participation, one batched call per module — never per party.
        var domainsByPartyId = new Dictionary<Guid, List<string>>();
        foreach (var contributor in _registryContributors)
        {
            var participants = await contributor.GetParticipantsAsync(partyIds, cancellationToken);
            foreach (var participantId in participants)
            {
                if (!domainsByPartyId.TryGetValue(participantId, out var list))
                {
                    domainsByPartyId[participantId] = list = [];
                }
                list.Add(contributor.DomainKey);
            }
        }

        // Spine-wide (ADR-011): every OrderType counts, aggregated in one query for the page.
        var orderAggregates = await _orders.GetPartyOrderAggregatesAsync(partyIds, cancellationToken);

        var personByPartyId = personProfiles
            .GroupBy(p => p.PartyId)
            .ToDictionary(g => g.Key, g => g.First());

        var businessByPartyId = businessProfiles
            .GroupBy(b => b.PartyId)
            .ToDictionary(g => g.Key, g => g.First());

        var listItems = parties.Select(party =>
        {
            primaryContacts.TryGetValue(party.Id, out var pc);
            personByPartyId.TryGetValue(party.Id, out var person);
            businessByPartyId.TryGetValue(party.Id, out var business);

            var canonicalPartyType = CanonicalizePartyType(party.PartyType);
            var verificationStatus = canonicalPartyType == "Business" ? business?.KybStatus : person?.IdvStatus;
            var photoUrlTiny = canonicalPartyType == "Person" ? person?.PhotoUrlTiny : null;

            var aggregate = orderAggregates.GetValueOrDefault(party.Id) ?? PartyOrderAggregate.Empty;

            return new CustomerListItem(
                party.Id,
                party.DisplayName,
                canonicalPartyType,
                party.Status,
                pc?.Email,
                pc?.Phone,
                photoUrlTiny,
                verificationStatus,
                party.CreatedAt,
                countryByPartyId.GetValueOrDefault(party.Id),
                domainsByPartyId.TryGetValue(party.Id, out var partyDomains)
                    ? partyDomains.OrderBy(d => d, StringComparer.Ordinal).ToList()
                    : [],
                aggregate.OrderCount,
                aggregate.TotalByCurrency
                    .Select(t => new CustomerRegistryCurrencyTotal(t.Currency, t.Amount))
                    .ToList());
        }).ToList();

        return new PagedResult<CustomerListItem>(
            listItems,
            totalCount,
            pageNumber,
            pageSize);
    }

    private static bool IsPersonPartyType(string? partyType)
    {
        return string.Equals(partyType, "Person", StringComparison.OrdinalIgnoreCase)
            || string.Equals(partyType, "Individual", StringComparison.OrdinalIgnoreCase);
    }

    private static string CanonicalizePartyType(string partyType)
    {
        return IsPersonPartyType(partyType) ? "Person" : partyType;
    }

    public async Task<CustomerDetail?> GetCustomerAsync(Guid partyId, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Customers.Read", cancellationToken);

        var tenantId = _tenantProvider.GetCurrentTenantId();

        var party = await _dbContext.Parties
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.Id == partyId)
            .Select(p => new
            {
                p.Id,
                p.DisplayName,
                p.PartyType,
                p.Status,
                p.CustomerTierCode,
                p.CreatedAt,
                p.UpdatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (party == null)
        {
            return null;
        }

        var personProfile = await _dbContext.PersonProfiles
            .AsNoTracking()
            .Where(pp => pp.PartyId == partyId)
            .Select(pp => new PersonProfileDetail(
                pp.Title,
                pp.FirstName,
                pp.LastName,
                pp.CountryCode,
                pp.PhotoUrl,
                pp.Dob,
                pp.Nationality,
                pp.Occupation,
                pp.IdvStatus))
            .FirstOrDefaultAsync(cancellationToken);

        var businessProfile = await _dbContext.BusinessProfiles
            .AsNoTracking()
            .Where(bp => bp.PartyId == partyId)
            .Select(bp => new BusinessProfileDetail(
                bp.RegistrationNumber,
                bp.IncorporationCountry,
                bp.Industry,
                bp.KybStatus))
            .FirstOrDefaultAsync(cancellationToken);

        var contacts = await _dbContext.PartyContacts
            .AsNoTracking()
            .Where(c => c.PartyId == partyId)
            .OrderByDescending(c => c.IsPrimary)
            .ThenBy(c => c.Type)
            .ThenBy(c => c.CreatedAt)
            .Select(c => new PartyContactDetail(
                c.Id,
                c.Type,
                c.Value,
                c.IsPrimary))
            .ToListAsync(cancellationToken);

        var addresses = await _dbContext.PartyAddresses
            .AsNoTracking()
            .Where(a => a.PartyId == partyId)
            .OrderBy(a => a.Type)
            .ThenBy(a => a.CreatedAt)
            .Select(a => new PartyAddressDetail(
                a.Id,
                a.Type,
                a.Line1,
                a.Line2,
                a.Line3,
                a.City,
                a.State,
                a.Postcode,
                a.Country))
            .ToListAsync(cancellationToken);

        var consents = await _dbContext.PartyConsents
            .AsNoTracking()
            .Where(c => c.PartyId == partyId)
            .OrderByDescending(c => c.GrantedAt)
            .Select(c => new PartyConsentDetail(
                c.Id,
                c.ConsentType,
                c.GrantedAt,
                c.RevokedAt))
            .ToListAsync(cancellationToken);

        var externalAccounts = await _dbContext.PartyAccounts
            .AsNoTracking()
            .Where(ea => ea.TenantId == tenantId && ea.PartyId == partyId)
            .OrderByDescending(ea => ea.CreatedAt)
            .Select(ea => new PartyAccountDetail(
                ea.Id,
                ea.AccountType,
                ea.MaskedIdentifier,
                ea.ProviderRef,
                ea.VerificationStatus,
                ea.Currency,
                ea.Country,
                ea.MetadataJson))
            .ToListAsync(cancellationToken);

        var roleAssignments = await _dbContext.PartyRoleAssignments
            .AsNoTracking()
            .Where(ra => ra.TenantId == tenantId && ra.PartyId == partyId)
            .OrderBy(ra => ra.Role)
            .ThenBy(ra => ra.ContextType)
            .ThenBy(ra => ra.CreatedAt)
            .Select(ra => new PartyRoleAssignmentDetail(
                ra.Id,
                ra.Role,
                ra.ContextType,
                ra.ContextId))
            .ToListAsync(cancellationToken);

        var relationships = await _dbContext.PartyRelationships
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId && (r.FromPartyId == partyId || r.ToPartyId == partyId))
            .OrderByDescending(r => r.IsActive)
            .ThenBy(r => r.RelationshipTypeCode)
            .ThenBy(r => r.CreatedAt)
            .Select(r => new PartyRelationshipDetail(
                r.Id,
                r.FromPartyId,
                r.ToPartyId,
                r.RelationshipTypeCode,
                r.IsActive,
                r.Notes))
            .ToListAsync(cancellationToken);

        var userId = await _dbContext.UserParties
            .AsNoTracking()
            .Where(up => up.PartyId == partyId)
            .Select(up => (Guid?)up.UserId)
            .FirstOrDefaultAsync(cancellationToken);

        return new CustomerDetail(
            party.Id,
            userId,
            party.DisplayName,
            party.PartyType,
            party.Status,
            party.CustomerTierCode,
            party.CreatedAt,
            party.UpdatedAt,
            personProfile,
            businessProfile,
            contacts,
            addresses,
            consents,
            externalAccounts,
            roleAssignments,
            relationships);
    }

    public async Task<CustomerStats?> GetCustomerStatsAsync(Guid partyId, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Customers.Read", cancellationToken);

        var tenantId = _tenantProvider.GetCurrentTenantId();

        var partyExists = await _dbContext.Parties
            .AsNoTracking()
            .AnyAsync(p => p.TenantId == tenantId && p.Id == partyId, cancellationToken);

        if (!partyExists)
        {
            return null;
        }

        var stats = await _financeStatsProvider.GetStatsAsync(tenantId, partyId, cancellationToken);

        var totalPaidByCurrency = stats.TotalPaidByCurrency
            .Select(c => new CurrencyAmount(c.Currency, c.Amount))
            .ToList();

        var outstandingByCurrency = stats.OutstandingByCurrency
            .Select(c => new CurrencyAmount(c.Currency, c.Amount))
            .ToList();

        var trailingTwelveMonthsByCurrency = stats.TrailingTwelveMonthsByCurrency
            .Select(c => new CurrencyAmount(c.Currency, c.Amount))
            .ToList();

        var trailingThirtyDaysByCurrency = stats.TrailingThirtyDaysByCurrency
            .Select(c => new CurrencyAmount(c.Currency, c.Amount))
            .ToList();

        return new CustomerStats(
            partyId,
            stats.TotalOrders,
            totalPaidByCurrency,
            outstandingByCurrency,
            stats.LastActivityAt,
            stats.OpenOrderCount,
            trailingTwelveMonthsByCurrency,
            trailingThirtyDaysByCurrency);
    }

    public async Task<IReadOnlyList<CustomerActivityEntryDto>?> GetCustomerActivityAsync(
        Guid partyId,
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Customers.Read", cancellationToken);

        if (take <= 0)
        {
            take = 20;
        }
        if (take > 100)
        {
            take = 100;
        }

        var tenantId = _tenantProvider.GetCurrentTenantId();

        var partyExists = await _dbContext.Parties
            .AsNoTracking()
            .AnyAsync(p => p.TenantId == tenantId && p.Id == partyId, cancellationToken);

        if (!partyExists)
        {
            return null;
        }

        // Pull a slightly larger page from each source so the merged top-N
        // doesn't drop a recent doc because too many old orders were
        // returned. Capped to keep the queries cheap.
        var perSource = Math.Min(Math.Max(take, 5), 25);

        var financeEntries = await _activityProvider.GetRecentActivityAsync(
            tenantId,
            partyId,
            perSource,
            cancellationToken);

        var auditRows = await _dbContext.AuditLogs
            .AsNoTracking()
            .Where(log =>
                log.TenantId == tenantId &&
                log.ResourceType == "Party" &&
                log.ResourceId == partyId)
            .OrderByDescending(log => log.Timestamp)
            .Take(perSource)
            .Select(log => new
            {
                log.Timestamp,
                log.Action,
                log.ActorType,
            })
            .ToListAsync(cancellationToken);

        // Spec 035 — documents are owned by Aonik.Documents; read the party's documents through
        // the SharedKernel reader contract instead of a direct DbSet (tenant scope is applied inside).
        var documentList = await _documentReader.ListDocumentsAsync(
            new ListDocumentsQuery(PageNumber: 1, PageSize: perSource, OwnerPartyId: partyId),
            cancellationToken);
        var documentRows = documentList.Items;

        var merged = new List<CustomerActivityEntryDto>(
            financeEntries.Count + auditRows.Count + documentRows.Count);

        foreach (var fe in financeEntries)
        {
            merged.Add(new CustomerActivityEntryDto(
                fe.Timestamp,
                fe.Kind,
                fe.Title,
                fe.Subtitle,
                fe.LinkPath));
        }

        foreach (var audit in auditRows)
        {
            merged.Add(new CustomerActivityEntryDto(
                audit.Timestamp,
                "audit_log",
                FormatAuditTitle(audit.Action),
                $"by {audit.ActorType}",
                LinkPath: null));
        }

        foreach (var doc in documentRows)
        {
            merged.Add(new CustomerActivityEntryDto(
                doc.CreatedAt,
                "document_uploaded",
                $"Document · {doc.DocumentType}",
                doc.Status,
                $"/documents/{doc.DocumentId}"));
        }

        return merged
            .OrderByDescending(entry => entry.Timestamp)
            .Take(take)
            .ToList();
    }

    private static string FormatAuditTitle(string action)
    {
        // Audit log Action values look like "Customer.Created" or
        // "Compliance.DocumentVerified". Prettify for display without
        // forcing every event author to also write a friendly label.
        if (string.IsNullOrWhiteSpace(action))
        {
            return "Audit event";
        }

        var parts = action.Split('.');
        if (parts.Length < 2)
        {
            return action;
        }

        var verb = SplitPascalCase(parts[^1]);
        var domain = parts[0];
        return $"{domain} · {verb.ToLowerInvariant()}";
    }

    private static string SplitPascalCase(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        var sb = new System.Text.StringBuilder(input.Length + 4);
        for (var i = 0; i < input.Length; i++)
        {
            if (i > 0 && char.IsUpper(input[i]) && !char.IsUpper(input[i - 1]))
            {
                sb.Append(' ');
            }
            sb.Append(input[i]);
        }
        return sb.ToString();
    }

    public async Task<CreateCustomerResponse> CreateCustomerAsync(
        CreateCustomerRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Customers.Create", cancellationToken);

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            throw new ArgumentException("Display name is required.", nameof(request.DisplayName));
        }

        if (string.IsNullOrWhiteSpace(request.PartyType))
        {
            throw new ArgumentException("Party type is required.", nameof(request.PartyType));
        }

        var tenantId = _tenantProvider.GetCurrentTenantId();
        var now = _clock.UtcNow;
        var status = string.IsNullOrWhiteSpace(request.Status) ? "Active" : request.Status.Trim();

        var party = new PartyEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PartyType = request.PartyType.Trim(),
            DisplayName = request.DisplayName.Trim(),
            Status = status,
            CustomerTierCode = request.CustomerTierCode?.Trim(),
            CreatedAt = now
        };

        if (request.Contacts != null && request.Contacts.Count > 0)
        {
            foreach (var contact in request.Contacts)
            {
                if (string.IsNullOrWhiteSpace(contact.Value))
                {
                    continue;
                }

                party.Contacts.Add(new PartyContact
                {
                    PartyId = party.Id,
                    Type = contact.Type.Trim(),
                    Value = contact.Value.Trim(),
                    IsPrimary = contact.IsPrimary,
                    CreatedAt = now
                });
            }
        }

        if (request.Addresses != null && request.Addresses.Count > 0)
        {
            foreach (var address in request.Addresses)
            {
                if (string.IsNullOrWhiteSpace(address.Line1))
                {
                    continue;
                }

                party.Addresses.Add(new PartyAddress
                {
                    PartyId = party.Id,
                    Type = address.Type.Trim(),
                    Line1 = address.Line1.Trim(),
                    Line2 = address.Line2?.Trim(),
                    Line3 = address.Line3?.Trim(),
                    City = address.City.Trim(),
                    State = address.State?.Trim(),
                    Postcode = address.Postcode.Trim(),
                    Country = address.Country.Trim(),
                    CreatedAt = now
                });
            }
        }

        if (string.Equals(party.PartyType, "Person", StringComparison.OrdinalIgnoreCase))
        {
            _dbContext.PersonProfiles.Add(new PersonProfile
            {
                PartyId = party.Id,
                Title = request.Title?.Trim(),
                FirstName = request.FirstName?.Trim(),
                LastName = request.LastName?.Trim(),
                Dob = request.Dob,
                Nationality = request.Nationality?.Trim(),
                Occupation = request.Occupation?.Trim(),
                CountryCode = request.CountryCode?.Trim(),
                IdvStatus = "Unverified",
                CreatedAt = now
            });
        }

        if (string.Equals(party.PartyType, "Business", StringComparison.OrdinalIgnoreCase))
        {
            _dbContext.BusinessProfiles.Add(new BusinessProfile
            {
                PartyId = party.Id,
                RegistrationNumber = request.RegistrationNumber?.Trim(),
                IncorporationCountry = request.IncorporationCountry?.Trim(),
                Industry = request.Industry?.Trim(),
                KybStatus = "Unverified",
                CreatedAt = now
            });
        }

        _dbContext.PartyRoleAssignments.Add(new PartyRoleAssignment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PartyId = party.Id,
            Role = PartyRoles.Customer,
            ContextType = "Tenant",
            ContextId = tenantId,
            CreatedAt = now
        });

        _dbContext.Parties.Add(party);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.LogAsync(
            AuditEventNames.CustomerCreated,
            "Party",
            party.Id,
            tenantId,
            actorId: null,
            correlationId: null,
            detailsJson: JsonSerializer.Serialize(new
            {
                party.Id,
                party.DisplayName,
                party.PartyType,
                party.Status
            }, JsonOptions),
            cancellationToken: cancellationToken);

        return new CreateCustomerResponse(
            party.Id,
            party.DisplayName,
            party.PartyType,
            party.Status,
            party.CreatedAt);
    }

}
