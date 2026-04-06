using ApiContracts = Aonik.Platform.Contracts.Api.Party;
using AppModels = Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Party;

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
        Summary(s =>
        {
            s.Summary = "Create a related party";
            s.Description = "Creates a new party and establishes a relationship to the specified parent party.";
            s.Response(200, "Related party created");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Parties"));
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
