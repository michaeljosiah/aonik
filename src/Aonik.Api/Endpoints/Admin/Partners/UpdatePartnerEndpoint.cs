using Aonik.Application.Models.Partners;
using Aonik.Application.Services.Partners;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Admin.Partners;

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
    }

    public override async Task HandleAsync(UpdatePartnerRequest req, CancellationToken ct)
    {
        var partnerId = Route<Guid>("partnerId");
        var result = await _partnerAdminService.UpdatePartnerAsync(partnerId, req, ct);
        await Send.OkAsync(result, ct);
    }
}
