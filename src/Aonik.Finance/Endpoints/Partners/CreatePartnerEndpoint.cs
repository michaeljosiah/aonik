using Aonik.Finance.Contracts.Models.Partners;
using Aonik.Finance.Contracts.Services.Partners;
using FastEndpoints;

namespace Aonik.Finance.Endpoints.Partners;

public class CreatePartnerEndpoint : Endpoint<CreatePartnerRequest, CreatePartnerResponse>
{
    private readonly IPartnerAdminService _partnerAdminService;

    public CreatePartnerEndpoint(IPartnerAdminService partnerAdminService)
    {
        _partnerAdminService = partnerAdminService;
    }

    public override void Configure()
    {
        Post("/admin/partners");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(CreatePartnerRequest req, CancellationToken ct)
    {
        var result = await _partnerAdminService.CreatePartnerAsync(req, ct);
        await Send.CreatedAtAsync<GetPartnerEndpoint>(
            routeValues: new { partnerId = result.PartnerId },
            responseBody: result,
            cancellation: ct);
    }
}
