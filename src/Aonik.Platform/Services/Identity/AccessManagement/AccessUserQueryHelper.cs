using Microsoft.EntityFrameworkCore;

using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Platform.Services.Identity.AccessManagement;

/// <summary>
/// Read-side queries for the access-management user surface
/// (list + detail). All methods are tenant-scoped and AsNoTracking.
/// </summary>
internal sealed class AccessUserQueryHelper
{
    private readonly PlatformDbContext _dbContext;
    private readonly IPermissionService _permissionService;

    public AccessUserQueryHelper(PlatformDbContext dbContext, IPermissionService permissionService)
    {
        _dbContext = dbContext;
        _permissionService = permissionService;
    }

    public async Task<PagedResult<AccessUserSummary>> ListUsersAsync(
        Guid tenantId,
        ListUsersRequest request,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Users
            .AsNoTracking()
            .Where(user => user.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim();
            // Spec 026 Part 1: "Invited" is a virtual stage represented
            // by the bootstrap-issuer marker on the placeholder row.
            // The Status column stays "Active" so the auth pipeline
            // can keep its single status guard, but operators see the
            // user as "Invited" in lists and detail until first login.
            if (string.Equals(status, "Invited", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(user => user.ExternalIssuer == "aonik-bootstrap");
            }
            else
            {
                query = query.Where(user =>
                    user.Status == status &&
                    user.ExternalIssuer != "aonik-bootstrap");
            }
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(user => (user.Email ?? string.Empty).Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(user => user.Email ?? string.Empty)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(user => new
            {
                user.Id,
                Email = user.Email ?? string.Empty,
                Status = user.ExternalIssuer == "aonik-bootstrap" ? "Invited" : user.Status,
                user.LastLoginAt,
                RoleCount = _dbContext.UserRoles.Count(ur => ur.UserId == user.Id),
                PartyInfo = _dbContext.UserParties
                    .Where(link => link.TenantId == tenantId && link.UserId == user.Id)
                    .Join(_dbContext.Parties,
                        link => link.PartyId,
                        party => party.Id,
                        (link, party) => new
                        {
                            PartyId = (Guid?)party.Id,
                            party.DisplayName,
                            party.PartyType,
                            link.LinkType,
                            link.CreatedAt,
                            PersonProfile = _dbContext.PersonProfiles
                                .Where(pp => pp.PartyId == party.Id)
                                .Select(pp => new
                                {
                                    pp.PhotoUrl,
                                    pp.PhotoUrlSmall,
                                    pp.PhotoUrlTiny
                                })
                                .FirstOrDefault()
                        })
                    .OrderBy(link => link.CreatedAt)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        var summaries = items.Select(item => new AccessUserSummary(
            item.Id,
            item.Email,
            null,
            item.Status,
            item.LastLoginAt,
            item.RoleCount,
            item.PartyInfo?.PartyId,
            item.PartyInfo?.DisplayName,
            item.PartyInfo?.PartyType,
            item.PartyInfo?.LinkType,
            item.PartyInfo?.PersonProfile?.PhotoUrl,
            item.PartyInfo?.PersonProfile?.PhotoUrlSmall,
            item.PartyInfo?.PersonProfile?.PhotoUrlTiny)).ToList();

        return new PagedResult<AccessUserSummary>(
            summaries,
            request.PageNumber,
            request.PageSize,
            totalCount);
    }

    public async Task<AccessUserDetail?> GetUserAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, cancellationToken);

        if (user == null)
        {
            return null;
        }

        var roles = await _dbContext.UserRoles
            .Where(ur => ur.UserId == userId)
            .Include(ur => ur.Role)
            .OrderBy(ur => ur.Role.Name)
            .Select(ur => new RoleSummary(ur.Role.Id, ur.Role.Name))
            .ToListAsync(cancellationToken);

        var permissions = await _permissionService.GetUserPermissionsAsync(userId, cancellationToken);

        // Load party info with extended details
        var partyLink = await _dbContext.UserParties
            .AsNoTracking()
            .Where(link => link.TenantId == tenantId && link.UserId == userId)
            .OrderBy(link => link.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        PersonProfileDetail? personProfile = null;
        BusinessProfileDetail? businessProfile = null;
        List<PartyContactDetail> contacts = new();
        List<PartyAddressDetail> addresses = new();
        Guid? partyId = null;
        string? partyDisplayName = null;
        string? partyType = null;
        string? linkType = null;

        if (partyLink != null)
        {
            var party = await _dbContext.Parties
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == partyLink.PartyId, cancellationToken);

            if (party != null)
            {
                partyId = party.Id;
                partyDisplayName = party.DisplayName;
                partyType = party.PartyType;
                linkType = partyLink.LinkType;

                // Load PersonProfile if party type is Individual or Person
                if (party.PartyType == "Individual" || party.PartyType == "Person")
                {
                    var personProfileEntity = await _dbContext.PersonProfiles
                        .AsNoTracking()
                        .FirstOrDefaultAsync(p => p.PartyId == party.Id, cancellationToken);

                    if (personProfileEntity != null)
                    {
                        personProfile = new PersonProfileDetail(
                            personProfileEntity.Title,
                            personProfileEntity.FirstName,
                            personProfileEntity.LastName,
                            personProfileEntity.CountryCode,
                            personProfileEntity.PhotoUrl,
                            personProfileEntity.Dob,
                            personProfileEntity.Nationality,
                            personProfileEntity.Occupation,
                            personProfileEntity.IdvStatus);
                    }
                }

                // Load BusinessProfile if party type is Business
                if (party.PartyType == "Business")
                {
                    var businessProfileEntity = await _dbContext.BusinessProfiles
                        .AsNoTracking()
                        .FirstOrDefaultAsync(p => p.PartyId == party.Id, cancellationToken);

                    if (businessProfileEntity != null)
                    {
                        businessProfile = new BusinessProfileDetail(
                            businessProfileEntity.RegistrationNumber,
                            businessProfileEntity.IncorporationCountry,
                            businessProfileEntity.Industry,
                            businessProfileEntity.KybStatus);
                    }
                }

                // Load contacts
                contacts = await _dbContext.PartyContacts
                    .AsNoTracking()
                    .Where(c => c.PartyId == party.Id)
                    .OrderByDescending(c => c.IsPrimary)
                    .ThenBy(c => c.Type)
                    .Select(c => new PartyContactDetail(c.Id, c.Type, c.Value, c.IsPrimary))
                    .ToListAsync(cancellationToken);

                // Load addresses
                addresses = await _dbContext.PartyAddresses
                    .AsNoTracking()
                    .Where(a => a.PartyId == party.Id)
                    .OrderBy(a => a.Type)
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
            }
        }

        var status = string.Equals(user.ExternalIssuer, "aonik-bootstrap", StringComparison.Ordinal)
            ? "Invited"
            : user.Status;

        return new AccessUserDetail(
            user.Id,
            user.Email ?? string.Empty,
            null,
            status,
            user.CreatedAt,
            user.LastLoginAt,
            roles,
            permissions,
            partyId,
            partyDisplayName,
            partyType,
            linkType,
            personProfile,
            businessProfile,
            contacts,
            addresses);
    }
}
