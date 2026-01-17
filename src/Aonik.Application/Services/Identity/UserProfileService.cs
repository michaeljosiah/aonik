using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Aonik.Application.Abstractions.Observability;
using Aonik.Application.Abstractions.Persistence;
using Aonik.Application.Models.Identity;
using Aonik.Application.Services.Compliance;
using Aonik.Domain.Identity.Entities;
using Aonik.Domain.Party.Entities;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Application.Services.Identity;

public class UserProfileService : IUserProfileService
{
    private readonly IAonikDbContext _dbContext;
    private readonly IAuditLogWriter _auditLogWriter;
    private readonly IClock _clock;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ICorrelationContext _correlationContext;

    public UserProfileService(
        IAonikDbContext dbContext,
        IAuditLogWriter auditLogWriter,
        IClock clock,
        ICurrentUserProvider currentUserProvider,
        ICorrelationContext correlationContext)
    {
        _dbContext = dbContext;
        _auditLogWriter = auditLogWriter;
        _clock = clock;
        _currentUserProvider = currentUserProvider;
        _correlationContext = correlationContext;
    }

    public async Task<CurrentUserSnapshot?> GetCurrentUserAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
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

    public async Task<CustomerProfile?> UpdateCustomerProfileAsync(
        Guid userId,
        Guid tenantId,
        CustomerProfileUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
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

        ApplyProfileUpdates(user, party, request);

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
                request.DisplayName,
                Email = AuditLogMasking.MaskEmail(request.Email),
                Phone = AuditLogMasking.MaskPhone(request.Phone)
            }),

            cancellationToken);

        return MapProfile(user, party);
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

    private void ApplyProfileUpdates(User user, Party party, CustomerProfileUpdateRequest request)
    {
        var now = _clock.UtcNow;
        var actorId = _currentUserProvider.GetCurrentUserId();

        if (!string.IsNullOrWhiteSpace(request.DisplayName))
        {
            party.DisplayName = request.DisplayName.Trim();
            party.UpdatedAt = now;
            party.UpdatedBy = actorId;
        }

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var normalizedEmail = request.Email.Trim();
            user.Email = normalizedEmail;
            user.UpdatedAt = now;
            user.UpdatedBy = actorId;
            UpsertContact(party, "Email", normalizedEmail, now, actorId);
        }

        if (!string.IsNullOrWhiteSpace(request.Phone))
        {
            var normalizedPhone = request.Phone.Trim();
            user.Phone = normalizedPhone;
            user.UpdatedAt = now;
            user.UpdatedBy = actorId;
            UpsertContact(party, "Phone", normalizedPhone, now, actorId);
        }

        if (request.Address != null)
        {
            UpsertAddress(party, request.Address, now, actorId);
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

    private static void UpsertAddress(
        Party party,
        CustomerAddress address,
        DateTime now,
        Guid? actorId)
    {
        var existing = party.Addresses.FirstOrDefault(a => a.Type == "Primary")
                       ?? party.Addresses.FirstOrDefault();

        if (existing == null)
        {
            existing = new PartyAddress
            {
                PartyId = party.Id,
                Type = "Primary",
                CreatedAt = now,
                CreatedBy = actorId
            };

            party.Addresses.Add(existing);
        }

        existing.Line1 = address.Line1.Trim();
        existing.Line2 = address.Line2?.Trim();
        existing.Line3 = address.Line3?.Trim();
        existing.City = address.City.Trim();
        existing.State = address.State?.Trim();
        existing.Postcode = address.Postcode.Trim();
        existing.Country = address.Country.Trim();
        existing.UpdatedAt = now;
        existing.UpdatedBy = actorId;
    }

    private static CustomerProfile MapProfile(User user, Party party)
    {
        var addressEntity = party.Addresses.FirstOrDefault(a => a.Type == "Primary")
                            ?? party.Addresses.FirstOrDefault();

        CustomerAddress? address = null;
        if (addressEntity != null)
        {
            address = new CustomerAddress(
                addressEntity.Line1,
                addressEntity.Line2,
                addressEntity.Line3,
                addressEntity.City,
                addressEntity.State,
                addressEntity.Postcode,
                addressEntity.Country);
        }

        return new CustomerProfile(
            party.Id,
            party.DisplayName,
            user.Email,
            user.Phone,
            address);

    }
}
