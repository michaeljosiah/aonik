using ApiContracts = Aonik.Api.Contracts.Party;
using AppModels = Aonik.Platform.Contracts.Models.Party;
using Aonik.Platform.Contracts.Services.Party;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Party;

public class CreateRelatedPartyEndpoint : Endpoint<ApiContracts.CreateRelatedPartyRequest, ApiContracts.RelatedPartyResponse>
{
    private readonly IPartyService _partyService;

    public CreateRelatedPartyEndpoint(IPartyService partyService)
    {
        _partyService = partyService;
    }

    public override void Configure()
    {
        Post("/parties/{partyId:guid}/relationships");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(ApiContracts.CreateRelatedPartyRequest req, CancellationToken ct)
    {
        var partyId = Route<Guid>("partyId");
        var request = new AppModels.CreateRelatedPartyRequest(
            partyId,
            req.RelationshipTypeCode,
            req.DisplayName,
            req.FirstName,
            req.LastName,
            req.Phone,
            req.Email,
            req.CountryCode,
            req.Notes);

        var result = await _partyService.CreateRelatedPartyAsync(request, ct);

        var response = new ApiContracts.RelatedPartyResponse(
            new ApiContracts.PartyResponse(
                result.Party.PartyId,
                result.Party.DisplayName,
                result.Party.PartyType,
                result.Party.Status),
            new ApiContracts.PartyRelationshipResponse(
                result.Relationship.RelationshipId,
                result.Relationship.FromPartyId,
                result.Relationship.FromPartyName,
                result.Relationship.ToPartyId,
                result.Relationship.ToPartyName,
                result.Relationship.RelationshipTypeCode,
                result.Relationship.RelationshipTypeName,
                result.Relationship.IsActive));

        await Send.OkAsync(response, ct);
    }
}
