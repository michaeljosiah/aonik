using Aonik.SharedKernel.Abstractions;
using Aonik.Finance.Contracts.Models.Partners;
using Aonik.Finance.Contracts.Services.Partners;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

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
        Summary(s =>
        {
            s.Summary = "List partners";
            s.Description = "Returns a paginated list of financial partners.";
            s.Response(200, "Partners retrieved successfully");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Partners"));
    }

    public override async Task HandleAsync(ListPartnersRequest req, CancellationToken ct)
    {
        var result = await _partnerAdminService.ListPartnersAsync(req, ct);
        await Send.OkAsync(result, ct);
    }
}
