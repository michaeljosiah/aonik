using Aonik.Finance.Contracts.Services.Partners;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.Partners;

public class DeletePartnerConnectorEndpoint : EndpointWithoutRequest
{
    private readonly IPartnerAdminService _partnerAdminService;

    public DeletePartnerConnectorEndpoint(IPartnerAdminService partnerAdminService)
    {
        _partnerAdminService = partnerAdminService;
    }

    public override void Configure()
    {
        Delete("/admin/partners/{partnerId}/connectors/{connectorId}");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Delete a partner connector";
            s.Response(204, "Partner connector deleted");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Partners"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var partnerId = Route<Guid>("partnerId");
        var connectorId = Route<Guid>("connectorId");
        await _partnerAdminService.DeleteConnectorAsync(partnerId, connectorId, ct);
        await Send.NoContentAsync(ct);
    }
}
