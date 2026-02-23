using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Application.Abstractions.Observability;
using Aonik.Application.Abstractions.Persistence;
using Aonik.Platform.Contracts.Services.Storage;
using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Application.Services;
using Aonik.Application.Services.Compliance;
using Aonik.Platform.Contracts.Services.Compliance;
using Aonik.Platform.Contracts.Services.Identity;
using Aonik.Platform.Entities.Identity;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Application.Services.Identity;

public class AccessManagementService : AdminServiceBase, IAccessManagementService
{
    private readonly IAonikDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IClock _clock;
    private readonly IAuditLogWriter _auditLogWriter;
    private readonly ICorrelationContext _correlationContext;
    private readonly IProfilePhotoStore _profilePhotoStore;

    public AccessManagementService(
        IAonikDbContext dbContext,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider,
        IPermissionService permissionService,
        IClock clock,
        IAuditLogWriter auditLogWriter,
        ICorrelationContext correlationContext,
        IProfilePhotoStore profilePhotoStore)
        : base(currentUserProvider, permissionService)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _clock = clock;
        _auditLogWriter = auditLogWriter;
        _correlationContext = correlationContext;
        _profilePhotoStore = profilePhotoStore;
    }

    public async Task<PagedResult<AccessUserSummary>> ListUsersAsync(
        ListUsersRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Users.Read", cancellationToken);
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var query = _dbContext.Users
            .AsNoTracking()
            .Where(user => user.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim();
            query = query.Where(user => user.Status == status);
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
                user.Status,
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

    public async Task<AccessUserDetail?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Users.Read", cancellationToken);
        var tenantId = _tenantProvider.GetCurrentTenantId();

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

        var permissions = await PermissionService.GetUserPermissionsAsync(userId, cancellationToken);

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

        return new AccessUserDetail(
            user.Id,
            user.Email ?? string.Empty,
            null,
            user.Status,
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

    public async Task InviteUserAsync(InviteUserRequest request, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Users.Invite", cancellationToken);
        throw new InvalidOperationException("User invitations are not supported yet.");
    }

    public async Task UpdateUserRolesAsync(
        Guid userId,
        UpdateUserRolesRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Users.Manage", cancellationToken);
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var userExists = await _dbContext.Users
            .AnyAsync(u => u.Id == userId && u.TenantId == tenantId, cancellationToken);

        if (!userExists)
        {
            throw new InvalidOperationException($"User {userId} not found in tenant {tenantId}");
        }

        var roleIds = request.RoleIds.Distinct().ToList();
        if (roleIds.Count > 0)
        {
            var rolesInTenant = await _dbContext.Roles
                .Where(role => role.TenantId == tenantId && roleIds.Contains(role.Id))
                .Select(role => role.Id)
                .ToListAsync(cancellationToken);

            if (rolesInTenant.Count != roleIds.Count)
            {
                throw new InvalidOperationException("One or more roles were not found in the tenant.");
            }
        }

        var existingRoles = await _dbContext.UserRoles
            .Where(ur => ur.UserId == userId)
            .ToListAsync(cancellationToken);

        var existingRoleIds = existingRoles.Select(ur => ur.RoleId).ToHashSet();
        var rolesToRemove = existingRoles.Where(ur => !roleIds.Contains(ur.RoleId)).ToList();
        var rolesToAdd = roleIds
            .Where(roleId => !existingRoleIds.Contains(roleId))
            .Select(roleId => new UserRole
            {
                UserId = userId,
                RoleId = roleId,
                CreatedAt = _clock.UtcNow,
                CreatedBy = CurrentUserProvider.GetCurrentUserId()
            })
            .ToList();

        var roleIdsForAudit = roleIds
            .Concat(rolesToRemove.Select(role => role.RoleId))
            .Distinct()
            .ToList();

        var roleLookup = roleIdsForAudit.Count == 0
            ? new Dictionary<Guid, string>()
            : await _dbContext.Roles
                .Where(role => role.TenantId == tenantId && roleIdsForAudit.Contains(role.Id))
                .Select(role => new { role.Id, role.Name })
                .ToDictionaryAsync(role => role.Id, role => role.Name, cancellationToken);

        if (rolesToRemove.Count > 0)
        {
            _dbContext.UserRoles.RemoveRange(rolesToRemove);
        }

        if (rolesToAdd.Count > 0)
        {
            _dbContext.UserRoles.AddRange(rolesToAdd);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var currentUserId = CurrentUserProvider.GetCurrentUserId();

        foreach (var role in rolesToAdd)
        {
            var roleName = roleLookup.TryGetValue(role.RoleId, out var name) ? name : string.Empty;
            await _auditLogWriter.LogAsync(
                AuditEventNames.UserRoleAssigned,
                "UserRole",
                role.Id,
                tenantId,
                currentUserId,
                _correlationContext.CorrelationId,
                JsonSerializer.Serialize(new { userId, roleId = role.RoleId, roleName }),
                cancellationToken);
        }

        foreach (var role in rolesToRemove)
        {
            var roleName = roleLookup.TryGetValue(role.RoleId, out var name) ? name : string.Empty;
            await _auditLogWriter.LogAsync(
                AuditEventNames.UserRoleRemoved,
                "UserRole",
                role.Id,
                tenantId,
                currentUserId,
                _correlationContext.CorrelationId,
                JsonSerializer.Serialize(new { userId, roleId = role.RoleId, roleName }),
                cancellationToken);
        }
    }

    public async Task UpdateUserProfileAsync(
        Guid userId,
        UpdateUserProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Users.Manage", cancellationToken);
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, cancellationToken);

        if (user == null)
        {
            throw new InvalidOperationException($"User {userId} not found in tenant {tenantId}");
        }

        // Get the user's party link
        var partyLink = await _dbContext.UserParties
            .Where(link => link.TenantId == tenantId && link.UserId == userId)
            .OrderBy(link => link.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (partyLink == null)
        {
            throw new InvalidOperationException($"User {userId} does not have a party linked");
        }

        var party = await _dbContext.Parties
            .FirstOrDefaultAsync(p => p.Id == partyLink.PartyId, cancellationToken);

        if (party == null)
        {
            throw new InvalidOperationException($"Party {partyLink.PartyId} not found");
        }

        // Only update PersonProfile for Individual parties (person profiles)
        if (party.PartyType == "Individual" || party.PartyType == "Person")
        {
            var personProfile = await _dbContext.PersonProfiles
                .FirstOrDefaultAsync(p => p.PartyId == party.Id, cancellationToken);

            if (personProfile != null)
            {
                // Update profile fields
                if (request.FirstName != null)
                {
                    personProfile.FirstName = request.FirstName;
                }

                if (request.LastName != null)
                {
                    personProfile.LastName = request.LastName;
                }

                if (request.Title != null)
                {
                    personProfile.Title = request.Title;
                }

                if (request.CountryCode != null)
                {
                    personProfile.CountryCode = request.CountryCode;
                }

                if (request.Nationality != null)
                {
                    personProfile.Nationality = request.Nationality;
                }

                if (request.Occupation != null)
                {
                    personProfile.Occupation = request.Occupation;
                }

                personProfile.UpdatedAt = _clock.UtcNow;
                personProfile.UpdatedBy = CurrentUserProvider.GetCurrentUserId();

                // Update party display name if first/last name changed
                if (request.FirstName != null || request.LastName != null)
                {
                    var firstName = request.FirstName ?? personProfile.FirstName ?? string.Empty;
                    var lastName = request.LastName ?? personProfile.LastName ?? string.Empty;
                    party.DisplayName = $"{firstName} {lastName}".Trim();
                    party.UpdatedAt = _clock.UtcNow;
                    party.UpdatedBy = CurrentUserProvider.GetCurrentUserId();
                }

                await _dbContext.SaveChangesAsync(cancellationToken);

                // Audit log
                await _auditLogWriter.LogAsync(
                    AuditEventNames.CustomerProfileUpdated,
                    "PersonProfile",
                    personProfile.Id,
                    tenantId,
                    CurrentUserProvider.GetCurrentUserId(),
                    _correlationContext.CorrelationId,
                    System.Text.Json.JsonSerializer.Serialize(new { userId, partyId = party.Id, request }),
                    cancellationToken);
            }
        }
    }

    public async Task<CustomerPhotoUploadResponse?> UploadUserPhotoAsync(
        Guid userId,
        Stream fileStream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Users.Manage", cancellationToken);
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, cancellationToken);

        if (user == null)
        {
            return null;
        }

        // Get the user's party link
        var partyLink = await _dbContext.UserParties
            .Where(link => link.TenantId == tenantId && link.UserId == userId)
            .OrderBy(link => link.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (partyLink == null)
        {
            return null;
        }

        var party = await _dbContext.Parties
            .FirstOrDefaultAsync(p => p.Id == partyLink.PartyId, cancellationToken);

        if (party == null)
        {
            return null;
        }

        // Only support photo uploads for Individual parties (person profiles)
        if (party.PartyType != "Individual" && party.PartyType != "Person")
        {
            throw new InvalidOperationException("Photo upload is only supported for person profiles");
        }

        var personProfile = await _dbContext.PersonProfiles
            .FirstOrDefaultAsync(p => p.PartyId == party.Id, cancellationToken);

        if (personProfile == null)
        {
            // Create person profile if it doesn't exist
            personProfile = new Platform.Entities.Party.PersonProfile
            {
                PartyId = party.Id,
                IdvStatus = "Pending",
                CreatedAt = _clock.UtcNow,
                CreatedBy = CurrentUserProvider.GetCurrentUserId()
            };
            _dbContext.PersonProfiles.Add(personProfile);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        // Upload photo to storage
        var uploadResult = await _profilePhotoStore.UploadCustomerPhotoAsync(
            tenantId,
            party.Id,
            contentType,
            fileStream,
            cancellationToken);

        personProfile.PhotoUrl = uploadResult.OriginalUrl;
        personProfile.PhotoUrlMedium = uploadResult.MediumThumbnailUrl;
        personProfile.PhotoUrlSmall = uploadResult.SmallThumbnailUrl;
        personProfile.PhotoUrlTiny = uploadResult.TinyThumbnailUrl;
        personProfile.UpdatedAt = _clock.UtcNow;
        personProfile.UpdatedBy = CurrentUserProvider.GetCurrentUserId();

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Audit log
        await _auditLogWriter.LogAsync(
            AuditEventNames.CustomerPhotoUpdated,
            "PersonProfile",
            personProfile.Id,
            tenantId,
            CurrentUserProvider.GetCurrentUserId(),
            _correlationContext.CorrelationId,
            JsonSerializer.Serialize(new 
            { 
                userId, 
                partyId = party.Id, 
                photoUrl = uploadResult.OriginalUrl, 
                mediumThumbUrl = uploadResult.MediumThumbnailUrl,
                smallThumbUrl = uploadResult.SmallThumbnailUrl,
                tinyThumbUrl = uploadResult.TinyThumbnailUrl
            }),
            cancellationToken);

        return new CustomerPhotoUploadResponse(uploadResult.OriginalUrl);
    }

    public async Task<CustomerPhotoDeleteResponse?> DeleteUserPhotoAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Users.Manage", cancellationToken);
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, cancellationToken);

        if (user == null)
        {
            return null;
        }

        // Get the user's party link
        var partyLink = await _dbContext.UserParties
            .Where(link => link.TenantId == tenantId && link.UserId == userId)
            .OrderBy(link => link.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (partyLink == null)
        {
            return null;
        }

        var party = await _dbContext.Parties
            .FirstOrDefaultAsync(p => p.Id == partyLink.PartyId, cancellationToken);

        if (party == null)
        {
            return null;
        }

        // Only support photo deletion for Individual parties (person profiles)
        if (party.PartyType != "Individual" && party.PartyType != "Person")
        {
            throw new InvalidOperationException("Photo deletion is only supported for person profiles");
        }

        var personProfile = await _dbContext.PersonProfiles
            .FirstOrDefaultAsync(p => p.PartyId == party.Id, cancellationToken);

        if (personProfile == null)
        {
            return null;
        }

        // Delete from storage if exists
        if (!string.IsNullOrWhiteSpace(personProfile.PhotoUrl))
        {
            await _profilePhotoStore.DeleteCustomerPhotoAsync(personProfile.PhotoUrl, cancellationToken);
        }

        personProfile.PhotoUrl = null;
        personProfile.UpdatedAt = _clock.UtcNow;
        personProfile.UpdatedBy = CurrentUserProvider.GetCurrentUserId();

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Audit log
        await _auditLogWriter.LogAsync(
            AuditEventNames.CustomerPhotoDeleted,
            "PersonProfile",
            personProfile.Id,
            tenantId,
            CurrentUserProvider.GetCurrentUserId(),
            _correlationContext.CorrelationId,
            JsonSerializer.Serialize(new { userId, partyId = party.Id }),
            cancellationToken);

        return new CustomerPhotoDeleteResponse("ok");
    }

    public async Task ActivateUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Users.Manage", cancellationToken);
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, cancellationToken);

        if (user == null)
        {
            throw new InvalidOperationException($"User {userId} not found in tenant {tenantId}");
        }

        if (user.Status == "Active")
        {
            return;
        }

        user.Status = "Active";
        user.UpdatedAt = _clock.UtcNow;
        user.UpdatedBy = CurrentUserProvider.GetCurrentUserId();
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeactivateUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Users.Deactivate", cancellationToken);
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, cancellationToken);

        if (user == null)
        {
            throw new InvalidOperationException($"User {userId} not found in tenant {tenantId}");
        }

        if (user.Status == "Deactivated")
        {
            return;
        }

        user.Status = "Deactivated";
        user.UpdatedAt = _clock.UtcNow;
        user.UpdatedBy = CurrentUserProvider.GetCurrentUserId();
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<PagedResult<AccessRoleSummary>> ListRolesAsync(
        ListRolesRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Roles.Read", cancellationToken);
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var query = _dbContext.Roles
            .AsNoTracking()
            .Where(role => role.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(role => role.Name.Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(role => role.Name)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(role => new AccessRoleSummary(
                role.Id,
                role.Name,
                null,
                _dbContext.RolePermissions.Count(rp => rp.RoleId == role.Id),
                _dbContext.UserRoles.Count(ur => ur.RoleId == role.Id)))
            .ToListAsync(cancellationToken);

        return new PagedResult<AccessRoleSummary>(
            items,
            request.PageNumber,
            request.PageSize,
            totalCount);
    }

    public async Task<AccessRoleDetail?> GetRoleAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Roles.Read", cancellationToken);
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var role = await _dbContext.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == roleId && r.TenantId == tenantId, cancellationToken);

        if (role == null)
        {
            return null;
        }

        return await BuildRoleDetailAsync(role, cancellationToken);
    }

    public async Task<AccessRoleDetail> CreateRoleAsync(
        CreateRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Roles.Create", cancellationToken);
        var tenantId = _tenantProvider.GetCurrentTenantId();

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Role name is required", nameof(request.Name));
        }

        var trimmedName = request.Name.Trim();

        var exists = await _dbContext.Roles
            .AnyAsync(role => role.TenantId == tenantId && role.Name == trimmedName, cancellationToken);

        if (exists)
        {
            throw new InvalidOperationException($"Role '{trimmedName}' already exists in tenant {tenantId}");
        }

        var userId = CurrentUserProvider.GetCurrentUserId();
        var now = _clock.UtcNow;

        var role = new Role
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = trimmedName,
            CreatedAt = now,
            CreatedBy = userId
        };

        _dbContext.Roles.Add(role);
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (request.PermissionKeys.Count > 0)
        {
            await UpdateRolePermissionsAsync(role.Id, new UpdateRolePermissionsRequest(request.PermissionKeys), cancellationToken);
        }

        return await BuildRoleDetailAsync(role, cancellationToken);
    }

    public async Task<AccessRoleDetail> UpdateRoleAsync(
        Guid roleId,
        UpdateRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Roles.Update", cancellationToken);
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var role = await _dbContext.Roles
            .FirstOrDefaultAsync(r => r.Id == roleId && r.TenantId == tenantId, cancellationToken);

        if (role == null)
        {
            throw new InvalidOperationException($"Role {roleId} not found in tenant {tenantId}");
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            var trimmedName = request.Name.Trim();
            var exists = await _dbContext.Roles
                .AnyAsync(r => r.TenantId == tenantId && r.Name == trimmedName && r.Id != roleId, cancellationToken);

            if (exists)
            {
                throw new InvalidOperationException($"Role '{trimmedName}' already exists in tenant {tenantId}");
            }

            role.Name = trimmedName;
        }

        role.UpdatedAt = _clock.UtcNow;
        role.UpdatedBy = CurrentUserProvider.GetCurrentUserId();
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await BuildRoleDetailAsync(role, cancellationToken);
    }

    public async Task DeleteRoleAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Roles.Delete", cancellationToken);
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var role = await _dbContext.Roles
            .FirstOrDefaultAsync(r => r.Id == roleId && r.TenantId == tenantId, cancellationToken);

        if (role == null)
        {
            throw new InvalidOperationException($"Role {roleId} not found in tenant {tenantId}");
        }

        var assignedUsers = await _dbContext.UserRoles
            .AnyAsync(ur => ur.RoleId == roleId, cancellationToken);

        if (assignedUsers)
        {
            throw new InvalidOperationException("Cannot delete a role that is assigned to users.");
        }

        _dbContext.Roles.Remove(role);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateRolePermissionsAsync(
        Guid roleId,
        UpdateRolePermissionsRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Roles.Update", cancellationToken);
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var role = await _dbContext.Roles
            .FirstOrDefaultAsync(r => r.Id == roleId && r.TenantId == tenantId, cancellationToken);

        if (role == null)
        {
            throw new InvalidOperationException($"Role {roleId} not found in tenant {tenantId}");
        }

        var permissionKeys = request.PermissionKeys
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(key => key.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var permissions = permissionKeys.Count == 0
            ? new List<Permission>()
            : await _dbContext.Permissions
                .Where(permission => permissionKeys.Contains(permission.Key))
                .ToListAsync(cancellationToken);

        if (permissions.Count != permissionKeys.Count)
        {
            throw new InvalidOperationException("One or more permissions were not found.");
        }

        var existing = await _dbContext.RolePermissions
            .Where(rp => rp.RoleId == roleId)
            .ToListAsync(cancellationToken);

        var existingIds = existing.Select(rp => rp.PermissionId).ToHashSet();
        var targetIds = permissions.Select(permission => permission.Id).ToHashSet();

        var toRemove = existing.Where(rp => !targetIds.Contains(rp.PermissionId)).ToList();
        var toAdd = targetIds
            .Where(permissionId => !existingIds.Contains(permissionId))
            .Select(permissionId => new RolePermission
            {
                RoleId = roleId,
                PermissionId = permissionId,
                CreatedAt = _clock.UtcNow,
                CreatedBy = CurrentUserProvider.GetCurrentUserId()
            })
            .ToList();

        if (toRemove.Count > 0)
        {
            _dbContext.RolePermissions.RemoveRange(toRemove);
        }

        if (toAdd.Count > 0)
        {
            _dbContext.RolePermissions.AddRange(toAdd);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<PermissionDefinition>> ListPermissionsAsync(CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Permissions.Read", cancellationToken);

        var permissions = await _dbContext.Permissions
            .AsNoTracking()
            .OrderBy(permission => permission.Key)
            .Select(permission => new PermissionDefinition(
                permission.Key,
                permission.Description,
                GetPermissionCategory(permission.Key)))
            .ToListAsync(cancellationToken);

        return permissions;
    }

    private async Task<AccessRoleDetail> BuildRoleDetailAsync(Role role, CancellationToken cancellationToken)
    {
        var permissions = await _dbContext.RolePermissions
            .AsNoTracking()
            .Where(rp => rp.RoleId == role.Id)
            .Include(rp => rp.Permission)
            .OrderBy(rp => rp.Permission.Key)
            .Select(rp => new PermissionDefinition(
                rp.Permission.Key,
                rp.Permission.Description,
                GetPermissionCategory(rp.Permission.Key)))
            .ToListAsync(cancellationToken);

        var users = await _dbContext.UserRoles
            .AsNoTracking()
            .Where(ur => ur.RoleId == role.Id)
            .Include(ur => ur.User)
            .OrderBy(ur => ur.User.Email)
            .Select(ur => new
            {
                ur.User.Id,
                Email = ur.User.Email ?? string.Empty,
                ur.User.Status,
                ur.User.LastLoginAt,
                RoleCount = _dbContext.UserRoles.Count(userRole => userRole.UserId == ur.User.Id),
                PartyInfo = _dbContext.UserParties
                    .Where(link => link.UserId == ur.User.Id && link.TenantId == role.TenantId)
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

        var userSummaries = users.Select(user => new AccessUserSummary(
            user.Id,
            user.Email,
            null,
            user.Status,
            user.LastLoginAt,
            user.RoleCount,
            user.PartyInfo?.PartyId,
            user.PartyInfo?.DisplayName,
            user.PartyInfo?.PartyType,
            user.PartyInfo?.LinkType,
            user.PartyInfo?.PersonProfile?.PhotoUrl,
            user.PartyInfo?.PersonProfile?.PhotoUrlSmall,
            user.PartyInfo?.PersonProfile?.PhotoUrlTiny)).ToList();

        return new AccessRoleDetail(
            role.Id,
            role.Name,
            null,
            permissions,
            userSummaries);
    }

    private static string GetPermissionCategory(string permissionKey)
    {
        if (string.IsNullOrWhiteSpace(permissionKey))
        {
            return "General";
        }

        var parts = permissionKey.Split('.', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[0] : "General";
    }

}
