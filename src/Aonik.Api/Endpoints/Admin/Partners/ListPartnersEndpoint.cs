using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Application.Models.Partners;
using Aonik.Application.Services.Partners;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Admin.Partners;

public class ListPartnersEndpoint : Endpoint<ListPartnersRequest, PagedResult<PartnerListItem>>
{
    private readonly IPartnerAdminService _partnerAdminService;

    public ListPartnersEndpoint(IPartnerAdminService partnerAdminService)
    {
        _partnerAdminService = partnerAdminService;
    }

    public override void Configure()
    {
        Get("/admin/partners");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(ListPartnersRequest req, CancellationToken ct)
    {
        var result = await _partnerAdminService.ListPartnersAsync(req, ct);
        await Send.OkAsync(result, ct);
    }
}
