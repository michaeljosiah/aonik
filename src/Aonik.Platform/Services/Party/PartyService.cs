using System.Text.Json;
using Microsoft.EntityFrameworkCore;

using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Compliance;
using Aonik.Platform.Contracts.Services.Compliance;
using Aonik.Platform.Entities.Party;
using PartyEntity = Aonik.Platform.Entities.Party.Party;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Caching;

namespace Aonik.Platform.Services.Party;

internal class PartyService : IPartyService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly PlatformDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IClock _clock;
    private readonly IAuditLogWriter _auditLogWriter;
    private readonly ICacheInvalidationPublisher _cacheInvalidationPublisher;

    public PartyService(
        PlatformDbContext dbContext,
        ITenantProvider tenantProvider,
        IClock clock,
        IAuditLogWriter auditLogWriter,
        ICacheInvalidationPublisher cacheInvalidationPublisher)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _clock = clock;
        _auditLogWriter = auditLogWriter;
        _cacheInvalidationPublisher = cacheInvalidationPublisher;
    }

    public async Task<PartyResponse> CreatePartyAsync(
        CreatePartyRequest request,
        CancellationToken cancellationToken = default)
    {
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
        var party = new PartyEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PartyType = request.PartyType.Trim(),
            DisplayName = request.DisplayName.Trim(),
            Status = "Active",
            CreatedAt = now
        };

        AddContacts(party, request.Email, request.Phone, now);

        if (IsPersonPartyType(request.PartyType))
        {
            _dbContext.PersonProfiles.Add(new PersonProfile
            {
                PartyId = party.Id,
                FirstName = request.FirstName?.Trim(),
                LastName = request.LastName?.Trim(),
                CountryCode = request.CountryCode?.Trim(),
                IdvStatus = "Unverified",
                CreatedAt = now
            });
        }

        if (string.Equals(request.PartyType, "Business", StringComparison.OrdinalIgnoreCase))
        {
            _dbContext.BusinessProfiles.Add(new BusinessProfile
            {
                PartyId = party.Id,
                IncorporationCountry = request.CountryCode?.Trim(),
                KybStatus = "Unverified",
                CreatedAt = now
            });
        }

        _dbContext.Parties.Add(party);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.LogAsync(
            AuditEventNames.PartyCreated,
            "Party",
            party.Id,
            tenantId,
            actorId: null,
            correlationId: null,
            detailsJson: JsonSerializer.Serialize(new
            {
                party.Id,
                party.DisplayName,
                party.PartyType
            }, JsonOptions),
            cancellationToken: cancellationToken);

        return new PartyResponse(party.Id, party.DisplayName, party.PartyType, party.Status);
    }

    public async Task<PartyResponse?> GetPartyAsync(Guid partyId, CancellationToken cancellationToken = default)
    {
        var party = await _dbContext.Parties
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Id == partyId, cancellationToken);

        if (party == null)
        {
            return null;
        }

        return new PartyResponse(party.Id, party.DisplayName, party.PartyType, party.Status);
    }

    public async Task<RelatedPartyResponse> CreateRelatedPartyAsync(
        CreateRelatedPartyRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.CustomerPartyId == Guid.Empty)
        {
            throw new ArgumentException("Customer party id is required.", nameof(request.CustomerPartyId));
        }

        if (string.IsNullOrWhiteSpace(request.RelationshipTypeCode))
        {
            throw new ArgumentException("Relationship type code is required.", nameof(request.RelationshipTypeCode));
        }

        if (!PartyRelationshipTypes.Codes.Contains(request.RelationshipTypeCode))
        {
            throw new InvalidOperationException($"Unknown relationship type '{request.RelationshipTypeCode}'.");
        }

        var displayName = ResolveDisplayName(request.DisplayName, request.FirstName, request.LastName);
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var now = _clock.UtcNow;

        var customerPartyExists = await _dbContext.Parties
            .AnyAsync(party => party.Id == request.CustomerPartyId, cancellationToken);

        if (!customerPartyExists)
        {
            throw new InvalidOperationException($"Customer party {request.CustomerPartyId} not found.");
        }

        var party = new PartyEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PartyType = "Individual",
            DisplayName = displayName,
            Status = "Active",
            CreatedAt = now
        };

        AddContacts(party, request.Email, request.Phone, now);

        _dbContext.PersonProfiles.Add(new PersonProfile
        {
            PartyId = party.Id,
            FirstName = request.FirstName?.Trim(),
            LastName = request.LastName?.Trim(),
            CountryCode = request.CountryCode?.Trim(),
            IdvStatus = "Unverified",
            CreatedAt = now
        });

        _dbContext.Parties.Add(party);

        var relationship = new PartyRelationship
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FromPartyId = request.CustomerPartyId,
            ToPartyId = party.Id,
            RelationshipTypeCode = request.RelationshipTypeCode.Trim(),
            Notes = request.Notes?.Trim(),
            IsActive = true,
            CreatedAt = now
        };

        _dbContext.PartyRelationships.Add(relationship);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _cacheInvalidationPublisher.PublishAsync(new CacheInvalidationEvent("personal-finance-graph"), cancellationToken);

        var parties = await LoadPartyNamesAsync(request.CustomerPartyId, party.Id, cancellationToken);
        var relationshipResponse = new PartyRelationshipResponse(
            relationship.Id,
            request.CustomerPartyId,
            parties.FromPartyName,
            party.Id,
            parties.ToPartyName,
            relationship.RelationshipTypeCode,
            relationship.RelationshipTypeCode,
            relationship.IsActive);

        return new RelatedPartyResponse(
            new PartyResponse(party.Id, party.DisplayName, party.PartyType, party.Status),
            relationshipResponse);
    }

    public async Task<PartyRelationshipResponse> CreateRelationshipAsync(
        CreatePartyRelationshipRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RelationshipTypeCode))
        {
            throw new ArgumentException("Relationship type code is required.", nameof(request.RelationshipTypeCode));
        }

        if (!PartyRelationshipTypes.Codes.Contains(request.RelationshipTypeCode))
        {
            throw new InvalidOperationException($"Unknown relationship type '{request.RelationshipTypeCode}'.");
        }

        var tenantId = _tenantProvider.GetCurrentTenantId();
        var relationship = new PartyRelationship
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FromPartyId = request.FromPartyId,
            ToPartyId = request.ToPartyId,
            RelationshipTypeCode = request.RelationshipTypeCode.Trim(),
            Notes = request.Notes?.Trim(),
            IsActive = true
        };

        _dbContext.PartyRelationships.Add(relationship);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _cacheInvalidationPublisher.PublishAsync(new CacheInvalidationEvent("personal-finance-graph"), cancellationToken);

        var parties = await LoadPartyNamesAsync(request.FromPartyId, request.ToPartyId, cancellationToken);
        return new PartyRelationshipResponse(
            relationship.Id,
            request.FromPartyId,
            parties.FromPartyName,
            request.ToPartyId,
            parties.ToPartyName,
            relationship.RelationshipTypeCode,
            relationship.RelationshipTypeCode,
            relationship.IsActive);
    }

    public async Task<IReadOnlyList<PartyRelationshipResponse>> GetRelationshipsAsync(
        Guid partyId,
        CancellationToken cancellationToken = default)
    {
        var relationships = await _dbContext.PartyRelationships
            .AsNoTracking()
            .Where(rel => rel.FromPartyId == partyId || rel.ToPartyId == partyId)
            .OrderByDescending(rel => rel.CreatedAt)
            .ToListAsync(cancellationToken);

        if (relationships.Count == 0)
        {
            return Array.Empty<PartyRelationshipResponse>();
        }

        var partyIds = relationships
            .SelectMany(rel => new[] { rel.FromPartyId, rel.ToPartyId })
            .Distinct()
            .ToList();

        var partyLookup = await _dbContext.Parties
            .AsNoTracking()
            .Where(party => partyIds.Contains(party.Id))
            .ToDictionaryAsync(party => party.Id, party => party.DisplayName, cancellationToken);

        return relationships.Select(rel => new PartyRelationshipResponse(
            rel.Id,
            rel.FromPartyId,
            partyLookup.TryGetValue(rel.FromPartyId, out var fromName) ? fromName : string.Empty,
            rel.ToPartyId,
            partyLookup.TryGetValue(rel.ToPartyId, out var toName) ? toName : string.Empty,
            rel.RelationshipTypeCode,
            rel.RelationshipTypeCode,
            rel.IsActive)).ToList();
    }

    public async Task AssignPartyRoleAsync(
        Guid partyId,
        string role,
        string contextType,
        Guid contextId,
        CancellationToken cancellationToken = default)
    {
        if (partyId == Guid.Empty)
        {
            throw new ArgumentException("Party id is required.", nameof(partyId));
        }

        if (string.IsNullOrWhiteSpace(role))
        {
            throw new ArgumentException("Role is required.", nameof(role));
        }

        var tenantId = _tenantProvider.GetCurrentTenantId();
        var normalizedRole = role.Trim();
        var normalizedContextType = string.IsNullOrWhiteSpace(contextType) ? "Tenant" : contextType.Trim();

        var existing = await _dbContext.PartyRoleAssignments
            .AnyAsync(assignment =>
                assignment.TenantId == tenantId &&
                assignment.PartyId == partyId &&
                assignment.Role == normalizedRole &&
                assignment.ContextType == normalizedContextType &&
                assignment.ContextId == contextId,
                cancellationToken);

        if (existing)
        {
            return;
        }

        _dbContext.PartyRoleAssignments.Add(new PartyRoleAssignment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PartyId = partyId,
            Role = normalizedRole,
            ContextType = normalizedContextType,
            ContextId = contextId,
            CreatedAt = _clock.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<(string FromPartyName, string ToPartyName)> LoadPartyNamesAsync(
        Guid fromPartyId,
        Guid toPartyId,
        CancellationToken cancellationToken)
    {
        var parties = await _dbContext.Parties
            .AsNoTracking()
            .Where(party => party.Id == fromPartyId || party.Id == toPartyId)
            .Select(party => new { party.Id, party.DisplayName })
            .ToListAsync(cancellationToken);

        var fromName = parties.FirstOrDefault(party => party.Id == fromPartyId)?.DisplayName ?? string.Empty;
        var toName = parties.FirstOrDefault(party => party.Id == toPartyId)?.DisplayName ?? string.Empty;
        return (fromName, toName);
    }

    private static bool IsPersonPartyType(string partyType)
    {
        return string.Equals(partyType, "Person", StringComparison.OrdinalIgnoreCase)
               || string.Equals(partyType, "Individual", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveDisplayName(string displayName, string? firstName, string? lastName)
    {
        var normalizedDisplayName = displayName?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedDisplayName))
        {
            return normalizedDisplayName;
        }

        var combinedName = $"{firstName} {lastName}".Trim();
        if (string.IsNullOrWhiteSpace(combinedName))
        {
            throw new ArgumentException("Display name is required.", nameof(displayName));
        }

        return combinedName;
    }

    private static void AddContacts(PartyEntity party, string? email, string? phone, DateTime now)
    {
        if (!string.IsNullOrWhiteSpace(email))
        {
            party.Contacts.Add(new PartyContact
            {
                PartyId = party.Id,
                Type = "Email",
                Value = email.Trim(),
                IsPrimary = true,
                CreatedAt = now
            });
        }

        if (!string.IsNullOrWhiteSpace(phone))
        {
            party.Contacts.Add(new PartyContact
            {
                PartyId = party.Id,
                Type = "Phone",
                Value = phone.Trim(),
                IsPrimary = string.IsNullOrWhiteSpace(email),
                CreatedAt = now
            });
        }
    }
}
