using Aonik.Application.Models.Partners;
using Aonik.Application.Services.Partners;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Admin.Partners;

public class GetPartnerEndpoint : EndpointWithoutRequest<PartnerDetail>
{
    private readonly IPartnerAdminService _partnerAdminService;

    public GetPartnerEndpoint(IPartnerAdminService partnerAdminService)
    {
        _partnerAdminService = partnerAdminService;
    }

    public override void Configure()
    {
        Get("/admin/partners/{partnerId}");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var partnerId = Route<Guid>("partnerId");
        var result = await _partnerAdminService.GetPartnerAsync(partnerId, ct);

        if (result == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(result, ct);
    }
}
