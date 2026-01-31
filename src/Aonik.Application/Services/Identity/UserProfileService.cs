using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

using Microsoft.EntityFrameworkCore;

using Aonik.Application.Abstractions.Authentication;
using Aonik.Application.Abstractions.Observability;
using Aonik.Application.Abstractions.Persistence;
using Aonik.Application.Abstractions.Settings;
using Aonik.Application.Abstractions.Storage;
using Aonik.Application.Models.Identity;
using Aonik.Application.Services.Compliance;
using Aonik.Application.Settings;
using Aonik.Domain.Identity.Entities;
using Aonik.Domain.Party.Entities;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Application.Services.Identity;

public class UserProfileService : IUserProfileService
{
    private static readonly Regex CountryCodeRegex = new("^[A-Z]{2}$", RegexOptions.Compiled);
    private static readonly Regex PhoneRegex = new("^\\+?[1-9]\\d{7,14}$", RegexOptions.Compiled);

    private readonly IAonikDbContext _dbContext;
    private readonly IAuditLogWriter _auditLogWriter;
    private readonly IClock _clock;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ICorrelationContext _correlationContext;
    private readonly IIdpAccountServiceFactory _idpAccountServiceFactory;
    private readonly ISettingProvider _settingProvider;
    private readonly IProfilePhotoStore _profilePhotoStore;
    private readonly IPermissionService _permissionService;

    public UserProfileService(
        IAonikDbContext dbContext,
        IAuditLogWriter auditLogWriter,
        IClock clock,
        ICurrentUserProvider currentUserProvider,
        ICorrelationContext correlationContext,
        IIdpAccountServiceFactory idpAccountServiceFactory,
        ISettingProvider settingProvider,
        IProfilePhotoStore profilePhotoStore,
        IPermissionService permissionService)
    {
        _dbContext = dbContext;
        _auditLogWriter = auditLogWriter;
        _clock = clock;
        _currentUserProvider = currentUserProvider;
        _correlationContext = correlationContext;
        _idpAccountServiceFactory = idpAccountServiceFactory;
        _settingProvider = settingProvider;
        _profilePhotoStore = profilePhotoStore;
        _permissionService = permissionService;
    }

    public async Task<CurrentUserSnapshot?> GetCurrentUserAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync(userId, "UserInfo.Read", cancellationToken);
        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(
                u => u.Id == userId && u.TenantId == tenantId,
                cancellationToken);

        if (user == null)
        {
            return null;
        }

        var party = await GetPrimaryPartyAsync(userId, tenantId, cancellationToken);

        return new CurrentUserSnapshot(
            user.Id,
            tenantId,
            user.Email,
            user.Phone,
            user.Status,
            party?.Id,
            party?.DisplayName);
    }

    public async Task<CustomerProfileResponse?> GetCustomerProfileAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync(userId, "UserInfo.Read", cancellationToken);
        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, cancellationToken);

        if (user == null)
        {
            return null;
        }

        var party = await GetPrimaryPartyAsync(userId, tenantId, cancellationToken, includeDetails: true);
        if (party == null)
        {
            return null;
        }

        var profile = await GetOrCreatePersonProfileAsync(party.Id, cancellationToken);
        return MapProfile(user, party, profile);
    }

    public async Task<CustomerProfileResponse?> UpdateCustomerProfileAsync(
        Guid userId,
        Guid tenantId,
        UpdateCustomerProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync(userId, "UserInfo.Update", cancellationToken);
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(
                u => u.Id == userId && u.TenantId == tenantId,
                cancellationToken);

        if (user == null)
        {
            return null;
        }

        var party = await GetPrimaryPartyAsync(userId, tenantId, cancellationToken, includeDetails: true);
        if (party == null)
        {
            return null;
        }

        var profile = await GetOrCreatePersonProfileAsync(party.Id, cancellationToken);
        ApplyProfileUpdates(user, party, profile, request);

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.LogAsync(
            AuditEventNames.CustomerProfileUpdated,
            "Party",
            party.Id,
            tenantId,
            userId,
            _correlationContext.CorrelationId,
            JsonSerializer.Serialize(new
            {
                user.Id,
                PartyId = party.Id,
                request.FirstName,
                request.LastName,
                request.Title,
                request.CountryCode,
                Phone = AuditLogMasking.MaskPhone(request.Phone)
            }),
            cancellationToken);

        return MapProfile(user, party, profile);
    }

    public async Task<CustomerProfileResponse?> UpdateCustomerEmailAsync(
        Guid userId,
        Guid tenantId,
        UpdateCustomerEmailRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync(userId, "UserInfo.Update", cancellationToken);
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, cancellationToken);

        if (user == null)
        {
            return null;
        }

        var party = await GetPrimaryPartyAsync(userId, tenantId, cancellationToken, includeDetails: true);
        if (party == null)
        {
            return null;
        }

        var normalizedCurrentEmail = NormalizeEmail(request.CurrentEmail);
        var normalizedNewEmail = NormalizeEmail(request.NewEmail);

        if (!string.Equals(user.Email ?? string.Empty, normalizedCurrentEmail, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Current email does not match authenticated user.");
        }

        var existing = await _dbContext.Users
            .AnyAsync(u => u.TenantId == tenantId && u.Email == normalizedNewEmail && u.Id != userId, cancellationToken);

        if (existing)
        {
            throw new InvalidOperationException("Email already in use.");
        }

        if (!normalizedNewEmail.Contains('@', StringComparison.Ordinal))
        {
            throw new ArgumentException("Email must be a valid RFC 5322 address.", nameof(request.NewEmail));
        }

        var provider = await _settingProvider.GetAsync(AuthSettingNames.Provider, cancellationToken) ?? "AzureAd";
        var accountService = _idpAccountServiceFactory.GetService(provider);
        await accountService.ValidatePasswordAsync(user, request.Password, cancellationToken);
        await accountService.UpdateEmailAsync(user, normalizedNewEmail, cancellationToken);

        var now = _clock.UtcNow;
        var actorId = _currentUserProvider.GetCurrentUserId();
        user.Email = normalizedNewEmail;
        user.UpdatedAt = now;
        user.UpdatedBy = actorId;
        UpsertContact(party, "Email", normalizedNewEmail, now, actorId);

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.LogAsync(
            AuditEventNames.CustomerEmailUpdated,
            "User",
            user.Id,
            tenantId,
            userId,
            _correlationContext.CorrelationId,
            JsonSerializer.Serialize(new
            {
                user.Id,
                PartyId = party.Id,
                Email = AuditLogMasking.MaskEmail(normalizedNewEmail)
            }),
            cancellationToken);

        var profile = await GetOrCreatePersonProfileAsync(party.Id, cancellationToken);
        return MapProfile(user, party, profile);
    }

    public async Task<UpdateCustomerPasswordResponse> UpdateCustomerPasswordAsync(
        Guid userId,
        Guid tenantId,
        UpdateCustomerPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync(userId, "UserInfo.Update", cancellationToken);
        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, cancellationToken);

        if (user == null)
        {
            throw new InvalidOperationException("Authenticated user not found.");
        }

        var provider = await _settingProvider.GetAsync(AuthSettingNames.Provider, cancellationToken) ?? "AzureAd";
        var accountService = _idpAccountServiceFactory.GetService(provider);
        await accountService.ValidatePasswordAsync(user, request.CurrentPassword, cancellationToken);
        await accountService.UpdatePasswordAsync(user, request.NewPassword, cancellationToken);

        await _auditLogWriter.LogAsync(
            AuditEventNames.CustomerPasswordUpdated,
            "User",
            user.Id,
            tenantId,
            userId,
            _correlationContext.CorrelationId,
            JsonSerializer.Serialize(new { user.Id }),
            cancellationToken);

        return new UpdateCustomerPasswordResponse("ok");
    }

    public async Task<CustomerPhotoUploadResponse?> UploadCustomerPhotoAsync(
        Guid userId,
        Guid tenantId,
        Stream fileStream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync(userId, "UserInfo.Update", cancellationToken);
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, cancellationToken);

        if (user == null)
        {
            return null;
        }

        var party = await GetPrimaryPartyAsync(userId, tenantId, cancellationToken);
        if (party == null)
        {
            return null;
        }

        var profile = await GetOrCreatePersonProfileAsync(party.Id, cancellationToken);

        var uploadResult = await _profilePhotoStore.UploadCustomerPhotoAsync(
            tenantId,
            party.Id,
            contentType,
            fileStream,
            cancellationToken);

        profile.PhotoUrl = uploadResult.OriginalUrl;
        profile.PhotoUrlMedium = uploadResult.MediumThumbnailUrl;
        profile.PhotoUrlSmall = uploadResult.SmallThumbnailUrl;
        profile.PhotoUrlTiny = uploadResult.TinyThumbnailUrl;
        profile.UpdatedAt = _clock.UtcNow;
        profile.UpdatedBy = _currentUserProvider.GetCurrentUserId();

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.LogAsync(
            AuditEventNames.CustomerPhotoUpdated,
            "PersonProfile",
            profile.Id,
            tenantId,
            userId,
            _correlationContext.CorrelationId,
            JsonSerializer.Serialize(new 
            { 
                party.Id, 
                photoUrl = profile.PhotoUrl, 
                mediumThumbUrl = uploadResult.MediumThumbnailUrl,
                smallThumbUrl = uploadResult.SmallThumbnailUrl,
                tinyThumbUrl = uploadResult.TinyThumbnailUrl
            }),
            cancellationToken);

        return new CustomerPhotoUploadResponse(uploadResult.OriginalUrl);
    }

    public async Task<CustomerPhotoDeleteResponse?> DeleteCustomerPhotoAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync(userId, "UserInfo.Update", cancellationToken);
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, cancellationToken);

        if (user == null)
        {
            return null;
        }

        var party = await GetPrimaryPartyAsync(userId, tenantId, cancellationToken);
        if (party == null)
        {
            return null;
        }

        var profile = await GetOrCreatePersonProfileAsync(party.Id, cancellationToken);
        if (!string.IsNullOrWhiteSpace(profile.PhotoUrl))
        {
            await _profilePhotoStore.DeleteCustomerPhotoAsync(profile.PhotoUrl, cancellationToken);
        }

        profile.PhotoUrl = null;
        profile.UpdatedAt = _clock.UtcNow;
        profile.UpdatedBy = _currentUserProvider.GetCurrentUserId();

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.LogAsync(
            AuditEventNames.CustomerPhotoDeleted,
            "PersonProfile",
            profile.Id,
            tenantId,
            userId,
            _correlationContext.CorrelationId,
            JsonSerializer.Serialize(new { party.Id }),
            cancellationToken);

        return new CustomerPhotoDeleteResponse("ok");
    }

    private async Task<Party?> GetPrimaryPartyAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken,
        bool includeDetails = false)
    {
        var partyId = await _dbContext.UserParties
            .Where(link => link.TenantId == tenantId && link.UserId == userId)
            .OrderByDescending(link => link.CreatedAt)
            .Select(link => (Guid?)link.PartyId)
            .FirstOrDefaultAsync(cancellationToken);

        if (!partyId.HasValue)
        {
            return null;
        }

        IQueryable<Party> query = _dbContext.Parties;
        if (includeDetails)
        {
            query = query
                .Include(party => party.Addresses)
                .Include(party => party.Contacts);
        }

        return await query
            .FirstOrDefaultAsync(party => party.Id == partyId.Value && party.TenantId == tenantId, cancellationToken);
    }

    private async Task<PersonProfile> GetOrCreatePersonProfileAsync(Guid partyId, CancellationToken cancellationToken)
    {
        var profile = await _dbContext.PersonProfiles
            .FirstOrDefaultAsync(p => p.PartyId == partyId, cancellationToken);

        if (profile != null)
        {
            return profile;
        }

        profile = new PersonProfile
        {
            PartyId = partyId,
            IdvStatus = "Pending",
            CreatedAt = _clock.UtcNow,
            CreatedBy = _currentUserProvider.GetCurrentUserId()
        };

        _dbContext.PersonProfiles.Add(profile);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return profile;
    }

    private void ApplyProfileUpdates(User user, Party party, PersonProfile profile, UpdateCustomerProfileRequest request)
    {
        var now = _clock.UtcNow;
        var actorId = _currentUserProvider.GetCurrentUserId();
        var updated = false;

        if (!string.IsNullOrWhiteSpace(request.FirstName))
        {
            profile.FirstName = request.FirstName.Trim();
            updated = true;
        }
        else if (request.FirstName != null)
        {
            profile.FirstName = null;
            updated = true;
        }

        if (!string.IsNullOrWhiteSpace(request.LastName))
        {
            profile.LastName = request.LastName.Trim();
            updated = true;
        }
        else if (request.LastName != null)
        {
            profile.LastName = null;
            updated = true;
        }

        if (!string.IsNullOrWhiteSpace(request.Title))
        {
            profile.Title = request.Title.Trim();
            updated = true;
        }
        else if (request.Title != null)
        {
            profile.Title = null;
            updated = true;
        }

        if (request.FirstName != null || request.LastName != null)
        {
            if (string.IsNullOrWhiteSpace(profile.FirstName) || string.IsNullOrWhiteSpace(profile.LastName))
            {
                throw new ArgumentException("FirstName and LastName are required when updating names.");
            }
        }

        if (!string.IsNullOrWhiteSpace(request.CountryCode))
        {
            var normalized = request.CountryCode.Trim().ToUpperInvariant();
            if (!CountryCodeRegex.IsMatch(normalized))
            {
                throw new ArgumentException("CountryCode must be ISO-3166-1 alpha-2.", nameof(request.CountryCode));
            }

            profile.CountryCode = normalized;
            updated = true;
        }
        else if (request.CountryCode != null)
        {
            profile.CountryCode = null;
            updated = true;
        }

        if (!string.IsNullOrWhiteSpace(request.Phone))
        {
            var normalized = NormalizePhone(request.Phone);
            if (!PhoneRegex.IsMatch(normalized))
            {
                throw new ArgumentException("Phone must be a valid E.164 value.", nameof(request.Phone));
            }

            user.Phone = normalized;
            user.UpdatedAt = now;
            user.UpdatedBy = actorId;
            UpsertContact(party, "Phone", normalized, now, actorId);
        }

        if (updated)
        {
            profile.UpdatedAt = now;
            profile.UpdatedBy = actorId;
        }

        if (updated)
        {
            party.DisplayName = BuildDisplayName(profile.Title, profile.FirstName, profile.LastName, party.DisplayName);
            if (!string.IsNullOrWhiteSpace(party.DisplayName))
            {
                party.UpdatedAt = now;
                party.UpdatedBy = actorId;
            }
        }
    }

    private static void UpsertContact(
        Party party,
        string type,
        string value,
        DateTime now,
        Guid? actorId)
    {
        var contact = party.Contacts
            .FirstOrDefault(c => c.Type == type && c.IsPrimary)
            ?? party.Contacts.FirstOrDefault(c => c.Type == type);

        if (contact == null)
        {
            contact = new PartyContact
            {
                PartyId = party.Id,
                Type = type,
                Value = value,
                IsPrimary = true,
                CreatedAt = now,
                CreatedBy = actorId
            };

            party.Contacts.Add(contact);
            return;
        }

        contact.Value = value;
        contact.IsPrimary = true;
        contact.UpdatedAt = now;
        contact.UpdatedBy = actorId;
    }

    private static CustomerProfileResponse MapProfile(User user, Party party, PersonProfile profile)
    {
        return new CustomerProfileResponse(
            party.Id,
            user.Id,
            party.TenantId,
            user.Email ?? string.Empty,
            profile.FirstName,
            profile.LastName,
            profile.Title,
            user.Phone,
            profile.CountryCode,
            profile.PhotoUrl);
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static string NormalizePhone(string phone) => phone.Trim();

    private static string BuildDisplayName(string? title, string? firstName, string? lastName, string? fallback)
    {
        var parts = new[] { title, firstName, lastName }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Select(part => part!.Trim())
            .ToArray();

        if (parts.Length == 0)
        {
            return fallback ?? string.Empty;
        }

        return string.Join(' ', parts);
    }

    private async Task EnsurePermissionAsync(Guid userId, string permissionKey, CancellationToken cancellationToken)
    {
        var hasPermission = await _permissionService.HasPermissionAsync(userId, permissionKey, cancellationToken);
        if (!hasPermission)
        {
            throw new InvalidOperationException($"Permission {permissionKey} is required.");
        }
    }
}
