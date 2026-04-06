using Aonik.Finance.Contracts.Models.Partners;
using Aonik.Finance.Contracts.Services.Partners;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

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
        Summary(s =>
        {
            s.Summary = "Create a new partner";
            s.Description = "Registers a new financial partner (e.g. payment processor or biller aggregator) in the system.";
            s.Response(201, "Partner created successfully");
            s.Response(400, "Invalid request data");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Partners"));
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
