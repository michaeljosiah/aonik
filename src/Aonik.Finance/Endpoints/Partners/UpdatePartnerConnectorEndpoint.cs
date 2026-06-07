using Aonik.Finance.Contracts.Models.Partners;
using Aonik.Finance.Contracts.Services.Partners;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.Partners;

public class UpdatePartnerConnectorEndpoint : Endpoint<UpdatePartnerConnectorRequest, PartnerDetail>
{
    private readonly IPartnerAdminService _partnerAdminService;

    public UpdatePartnerConnectorEndpoint(IPartnerAdminService partnerAdminService)
    {
        _partnerAdminService = partnerAdminService;
    }

    public override void Configure()
    {
        Patch("/admin/partners/{partnerId}/connectors/{connectorId}");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Update a partner connector";
            s.Response(200, "Partner connector updated");
            s.Response(400, "Invalid request data");
            s.Response(401, "Not authenticated");
            s.Response(404, "Connector not found");
        });
        Options(x => x.WithTags("Partners"));
    }

    public override async Task HandleAsync(UpdatePartnerConnectorRequest req, CancellationToken ct)
    {
        var partnerId = Route<Guid>("partnerId");
        var connectorId = Route<Guid>("connectorId");
        var result = await _partnerAdminService.UpdateConnectorAsync(partnerId, connectorId, req, ct);
        await Send.OkAsync(result, ct);
    }
}
