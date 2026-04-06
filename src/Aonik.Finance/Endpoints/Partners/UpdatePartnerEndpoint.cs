using Aonik.Finance.Contracts.Models.Partners;
using Aonik.Finance.Contracts.Services.Partners;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.Partners;

public class UpdatePartnerEndpoint : Endpoint<UpdatePartnerRequest, PartnerDetail>
{
    private readonly IPartnerAdminService _partnerAdminService;

    public UpdatePartnerEndpoint(IPartnerAdminService partnerAdminService)
    {
        _partnerAdminService = partnerAdminService;
    }

    public override void Configure()
    {
        Patch("/admin/partners/{partnerId}");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Update a partner";
            s.Description = "Partially updates an existing partner's configuration and details.";
            s.Response(200, "Partner updated successfully");
            s.Response(400, "Invalid request data");
            s.Response(401, "Not authenticated");
            s.Response(404, "Partner not found");
        });
        Options(x => x.WithTags("Partners"));
    }

    public override async Task HandleAsync(UpdatePartnerRequest req, CancellationToken ct)
    {
        var partnerId = Route<Guid>("partnerId");
        var result = await _partnerAdminService.UpdatePartnerAsync(partnerId, req, ct);
        await Send.OkAsync(result, ct);
    }
}
