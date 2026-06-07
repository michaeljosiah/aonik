using Aonik.Finance.Contracts.Models.Partners;
using Aonik.Finance.Contracts.Services.Partners;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.Partners;

public class CreatePartnerConnectorEndpoint : Endpoint<CreatePartnerConnectorRequest, PartnerDetail>
{
    private readonly IPartnerAdminService _partnerAdminService;

    public CreatePartnerConnectorEndpoint(IPartnerAdminService partnerAdminService)
    {
        _partnerAdminService = partnerAdminService;
    }

    public override void Configure()
    {
        Post("/admin/partners/{partnerId}/connectors");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Create a partner connector";
            s.Description = "Adds a connector row for a partner. CredentialsRef points at gateway settings; it never carries a secret.";
            s.Response(200, "Partner connector created");
            s.Response(400, "Invalid request data");
            s.Response(401, "Not authenticated");
            s.Response(404, "Partner not found");
        });
        Options(x => x.WithTags("Partners"));
    }

    public override async Task HandleAsync(CreatePartnerConnectorRequest req, CancellationToken ct)
    {
        var partnerId = Route<Guid>("partnerId");
        var result = await _partnerAdminService.CreateConnectorAsync(partnerId, req, ct);
        await Send.OkAsync(result, ct);
    }
}
