using Aonik.Platform.Contracts.Api.Party;
using Aonik.SharedKernel.Abstractions;
using FastEndpoints;

using ApiCreatePartyRequest = Aonik.Platform.Contracts.Api.Party.CreatePartyRequest;
using ApiPartyResponse = Aonik.Platform.Contracts.Api.Party.PartyResponse;

namespace Aonik.Platform.Endpoints.Party;

public class CreatePartyEndpoint : Endpoint<ApiCreatePartyRequest, ApiPartyResponse>
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

    public override async Task HandleAsync(ApiCreatePartyRequest req, CancellationToken ct)
    {
        var request = new Aonik.SharedKernel.Abstractions.CreatePartyRequest(
            req.DisplayName,
            req.PartyType,
            req.FirstName,
            req.LastName,
            req.Phone,
            req.Email,
            req.CountryCode);

        var result = await _partyService.CreatePartyAsync(request, ct);
        var response = new ApiPartyResponse(
            result.PartyId,
            result.DisplayName,
            result.PartyType,
            result.Status);

        await Send.OkAsync(response, ct);
    }
}
