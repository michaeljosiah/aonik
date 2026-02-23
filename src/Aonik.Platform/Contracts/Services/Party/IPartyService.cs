using Aonik.Platform.Contracts.Models.Party;

namespace Aonik.Platform.Contracts.Services.Party;

public interface IPartyService
{
    Task<PartyResponse> CreatePartyAsync(
        CreatePartyRequest request,
        CancellationToken cancellationToken = default);

    Task<PartyResponse?> GetPartyAsync(
        Guid partyId,
        CancellationToken cancellationToken = default);

    Task<RelatedPartyResponse> CreateRelatedPartyAsync(
        CreateRelatedPartyRequest request,
        CancellationToken cancellationToken = default);

    Task<PartyRelationshipResponse> CreateRelationshipAsync(
        CreatePartyRelationshipRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PartyRelationshipResponse>> GetRelationshipsAsync(
        Guid partyId,
        CancellationToken cancellationToken = default);
}
