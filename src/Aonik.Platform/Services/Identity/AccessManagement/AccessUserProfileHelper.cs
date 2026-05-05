using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Compliance;
using Aonik.Platform.Contracts.Services.Storage;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Compliance;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Observability;

namespace Aonik.Platform.Services.Identity.AccessManagement;

/// <summary>
/// User-facing party-profile maintenance: name/title/country fields,
/// plus profile photo upload/delete. Limited to Individual/Person
/// parties — business profiles are out of scope here.
/// </summary>
internal sealed class AccessUserProfileHelper
{
    private readonly PlatformDbContext _dbContext;
    private readonly IClock _clock;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IAuditLogWriter _auditLogWriter;
    private readonly ICorrelationContext _correlationContext;
    private readonly IProfilePhotoStore _profilePhotoStore;

    public AccessUserProfileHelper(
        PlatformDbContext dbContext,
        IClock clock,
        ICurrentUserProvider currentUserProvider,
        IAuditLogWriter auditLogWriter,
        ICorrelationContext correlationContext,
        IProfilePhotoStore profilePhotoStore)
    {
        _dbContext = dbContext;
        _clock = clock;
        _currentUserProvider = currentUserProvider;
        _auditLogWriter = auditLogWriter;
        _correlationContext = correlationContext;
        _profilePhotoStore = profilePhotoStore;
    }

    public async Task UpdateUserProfileAsync(
        Guid tenantId,
        Guid userId,
        UpdateUserProfileRequest request,
        CancellationToken cancellationToken)
    {
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
                personProfile.UpdatedBy = _currentUserProvider.GetCurrentUserId();

                // Update party display name if first/last name changed
                if (request.FirstName != null || request.LastName != null)
                {
                    var firstName = request.FirstName ?? personProfile.FirstName ?? string.Empty;
                    var lastName = request.LastName ?? personProfile.LastName ?? string.Empty;
                    party.DisplayName = $"{firstName} {lastName}".Trim();
                    party.UpdatedAt = _clock.UtcNow;
                    party.UpdatedBy = _currentUserProvider.GetCurrentUserId();
                }

                await _dbContext.SaveChangesAsync(cancellationToken);

                // Audit log
                await _auditLogWriter.LogAsync(
                    AuditEventNames.CustomerProfileUpdated,
                    "PersonProfile",
                    personProfile.Id,
                    tenantId,
                    _currentUserProvider.GetCurrentUserId(),
                    _correlationContext.CorrelationId,
                    JsonSerializer.Serialize(new { userId, partyId = party.Id, request }),
                    cancellationToken);
            }
        }
    }

    public async Task<CustomerPhotoUploadResponse?> UploadUserPhotoAsync(
        Guid tenantId,
        Guid userId,
        Stream fileStream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken)
    {
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
                CreatedBy = _currentUserProvider.GetCurrentUserId()
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
        personProfile.UpdatedBy = _currentUserProvider.GetCurrentUserId();

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Audit log
        await _auditLogWriter.LogAsync(
            AuditEventNames.CustomerPhotoUpdated,
            "PersonProfile",
            personProfile.Id,
            tenantId,
            _currentUserProvider.GetCurrentUserId(),
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
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken)
    {
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
        personProfile.UpdatedBy = _currentUserProvider.GetCurrentUserId();

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Audit log
        await _auditLogWriter.LogAsync(
            AuditEventNames.CustomerPhotoDeleted,
            "PersonProfile",
            personProfile.Id,
            tenantId,
            _currentUserProvider.GetCurrentUserId(),
            _correlationContext.CorrelationId,
            JsonSerializer.Serialize(new { userId, partyId = party.Id }),
            cancellationToken);

        return new CustomerPhotoDeleteResponse("ok");
    }
}
