using Aonik.Api.Contracts.Party;
using Aonik.Application.Services.Parties;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Party;

public class CreatePartyEndpoint : Endpoint<CreatePartyRequest, PartyResponse>
{
    private readonly IPartyService _partyService;

    public CreatePartyEndpoint(IPartyService partyService)
    {
        _partyService = partyService;
    }

    public override void Configure()
    {
        Post("/parties");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(CreatePartyRequest req, CancellationToken ct)
    {
        var request = new Application.Models.Party.CreatePartyRequest(
            req.DisplayName,
            req.PartyType,
            req.FirstName,
            req.LastName,
            req.Phone,
            req.Email,
            req.CountryCode);

        var result = await _partyService.CreatePartyAsync(request, ct);
        var response = new PartyResponse(
            result.PartyId,
            result.DisplayName,
            result.PartyType,
            result.Status);

        await Send.OkAsync(response, ct);
    }
}
