using System.Text.Json;
using Microsoft.EntityFrameworkCore;

using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Application.Abstractions.Persistence;
using Aonik.Application.Models.Customers;
using Aonik.Application.Models.Identity;
using Aonik.Application.Services;
using Aonik.Application.Services.Compliance;
using Aonik.Application.Services.Identity;
using Aonik.Domain.Orders;
using Aonik.Domain.Payments;
using Aonik.Domain.Party;
using Aonik.Domain.Party.Entities;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Application.Services.Customers;

public class CustomerAdminService : AdminServiceBase, ICustomerAdminService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IAonikDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IClock _clock;
    private readonly IAuditLogWriter _auditLogWriter;

    public CustomerAdminService(
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

    public async Task<PagedResult<CustomerListItem>> ListCustomersAsync(
        ListCustomersRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Customers.Read", cancellationToken);

        var tenantId = _tenantProvider.GetCurrentTenantId();
        var pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;
        var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;

        var query = _dbContext.Parties
            .AsNoTracking()
            .Where(party => party.TenantId == tenantId)
            .Where(party => _dbContext.PartyRoleAssignments.Any(ra =>
                ra.PartyId == party.Id &&
                ra.TenantId == tenantId &&
                ra.Role == PartyRoles.Customer &&
                ra.ContextType == "Tenant" &&
                ra.ContextId == tenantId));

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

            return new CustomerListItem(
                party.Id,
                party.DisplayName,
                canonicalPartyType,
                party.Status,
                pc?.Email,
                pc?.Phone,
                photoUrlTiny,
                verificationStatus,
                party.CreatedAt);
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

        var externalAccounts = await _dbContext.ExternalAccounts
            .AsNoTracking()
            .Where(ea => ea.TenantId == tenantId && ea.PartyId == partyId)
            .OrderByDescending(ea => ea.CreatedAt)
            .Select(ea => new ExternalAccountDetail(
                ea.Id,
                ea.ExternalAccountType,
                ea.MaskedIdentifier,
                ea.ProviderRef,
                ea.VerificationStatus,
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

        return new CustomerDetail(
            party.Id,
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

        var orders = await _dbContext.Orders
            .AsNoTracking()
            .Where(order => order.TenantId == tenantId)
            .Where(order =>
                order.PayerPartyId == partyId ||
                _dbContext.OrderPartyRoles.Any(role =>
                    role.TenantId == tenantId && role.PartyId == partyId && role.OrderId == order.Id))
            .Select(order => new
            {
                order.Id,
                order.AmountIn,
                order.CurrencyIn,
                order.Status,
                order.CreatedAt,
                order.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        var totalOrders = orders.Select(order => order.Id).Distinct().Count();

        var terminalStatuses = new[]
        {
            OrderStatuses.Complete,
            OrderStatuses.Cancelled,
            OrderStatuses.Failed,
            OrderStatuses.Expired
        };

        var outstandingByCurrency = orders
            .Where(order => !terminalStatuses.Contains(order.Status))
            .GroupBy(order => order.CurrencyIn)
            .Select(group => new CurrencyAmount(group.Key, group.Sum(order => order.AmountIn)))
            .ToList();

        var capturedPayments = await _dbContext.PaymentIntents
            .AsNoTracking()
            .Where(intent => intent.TenantId == tenantId)
            .Where(intent => intent.PayerPartyId == partyId && intent.Status == PaymentStatus.Captured.ToString())
            .Select(intent => new
            {
                intent.Amount,
                intent.Currency
            })
            .ToListAsync(cancellationToken);

        var totalPaidByCurrency = capturedPayments
            .GroupBy(payment => payment.Currency)
            .Select(group => new CurrencyAmount(group.Key, group.Sum(payment => payment.Amount)))
            .ToList();

        var orderActivityAt = orders.Count == 0
            ? (DateTime?)null
            : orders.Max(order => order.UpdatedAt ?? order.CreatedAt);

        var paymentActivityAt = await _dbContext.PaymentIntents
            .AsNoTracking()
            .Where(intent => intent.TenantId == tenantId && intent.PayerPartyId == partyId)
            .MaxAsync(intent => (DateTime?)intent.CreatedAt, cancellationToken);

        var lastActivityAt = orderActivityAt;
        if (paymentActivityAt.HasValue && (!lastActivityAt.HasValue || paymentActivityAt > lastActivityAt))
        {
            lastActivityAt = paymentActivityAt;
        }

        return new CustomerStats(
            partyId,
            totalOrders,
            totalPaidByCurrency,
            outstandingByCurrency,
            lastActivityAt);
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

        var party = new Party
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
