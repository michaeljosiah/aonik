using Aonik.Finance.Contracts.Services.Partners;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.Partners;

public class DeletePartnerEndpoint : EndpointWithoutRequest
{
    private readonly IPartnerAdminService _partnerAdminService;

    public DeletePartnerEndpoint(IPartnerAdminService partnerAdminService)
    {
        _partnerAdminService = partnerAdminService;
    }

    public override void Configure()
    {
        Delete("/admin/partners/{partnerId}");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Delete a partner";
            s.Description = "Removes a partner from the system.";
            s.Response(204, "Partner deleted successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "Partner not found");
        });
        Options(x => x.WithTags("Partners"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var partnerId = Route<Guid>("partnerId");
        await _partnerAdminService.DeletePartnerAsync(partnerId, ct);
        await Send.NoContentAsync(ct);
    }
}
