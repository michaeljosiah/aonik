using Microsoft.EntityFrameworkCore;

using Aonik.Application.Abstractions.Multitenancy;
using Aonik.Application.Abstractions.Persistence;
using Aonik.Application.Models.Customers;
using Aonik.Application.Models.Identity;
using Aonik.Application.Services;
using Aonik.Application.Services.Identity;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Application.Services.Customers;

public class CustomerAdminService : AdminServiceBase, ICustomerAdminService
{
    private readonly IAonikDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;

    public CustomerAdminService(
        IAonikDbContext dbContext,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider,
        IPermissionService permissionService)
        : base(currentUserProvider, permissionService)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
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
            .Where(party => party.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim();
            query = query.Where(party => party.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(request.PartyType))
        {
            var partyType = request.PartyType.Trim();
            query = query.Where(party => party.PartyType == partyType);
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

            var verificationStatus = party.PartyType == "Business" ? business?.KybStatus : person?.IdvStatus;
            var photoUrlTiny = party.PartyType == "Person" ? person?.PhotoUrlTiny : null;

            return new CustomerListItem(
                party.Id,
                party.DisplayName,
                party.PartyType,
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

}
