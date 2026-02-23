using Aonik.SharedKernel.Abstractions;
using Aonik.Finance.Contracts.Models.Partners;
using Aonik.Finance.Contracts.Services.Partners;
using FastEndpoints;

namespace Aonik.Finance.Endpoints.Partners;

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
