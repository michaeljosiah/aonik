using Aonik.Platform.Contracts.Api.Party;
using Aonik.SharedKernel.Abstractions;
using FastEndpoints;

using ApiCreatePartyRequest = Aonik.Platform.Contracts.Api.Party.CreatePartyRequest;
using ApiPartyResponse = Aonik.Platform.Contracts.Api.Party.PartyResponse;
using Microsoft.AspNetCore.Http;

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
        Policies("AdminWritePolicy");
        Summary(s =>
        {
            s.Summary = "Create a new party";
            s.Description = "Creates a new party (individual or organization) with basic contact details.";
            s.Response(200, "Party created");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Parties"));
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
