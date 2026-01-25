using System.Text.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

using Aonik.Application.Abstractions.Multitenancy;
using Aonik.Application.Abstractions.Persistence;
using Aonik.Application.Models.Party;
using Aonik.Application.Services.Compliance;
using Aonik.Domain.Party.Entities;
using PartyEntity = Aonik.Domain.Party.Entities.Party;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Application.Services.Parties;

public class PartyService : IPartyService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IAonikDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IClock _clock;
    private readonly IAuditLogWriter _auditLogWriter;

    public PartyService(
        IAonikDbContext dbContext,
        ITenantProvider tenantProvider,
        IClock clock,
        IAuditLogWriter auditLogWriter)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _clock = clock;
        _auditLogWriter = auditLogWriter;
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

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            party.Contacts.Add(new PartyContact
            {
                PartyId = party.Id,
                Type = "Email",
                Value = request.Email.Trim(),
                IsPrimary = true,
                CreatedAt = now
            });
        }

        if (!string.IsNullOrWhiteSpace(request.Phone))
        {
            party.Contacts.Add(new PartyContact
            {
                PartyId = party.Id,
                Type = "Phone",
                Value = request.Phone.Trim(),
                IsPrimary = string.IsNullOrWhiteSpace(request.Email),
                CreatedAt = now
            });
        }

        if (string.Equals(request.PartyType, "Person", StringComparison.OrdinalIgnoreCase))
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

    public async Task<PartyRelationshipResponse> CreateRelationshipAsync(
        CreatePartyRelationshipRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RelationshipTypeCode))
        {
            throw new ArgumentException("Relationship type code is required.", nameof(request.RelationshipTypeCode));
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
}
