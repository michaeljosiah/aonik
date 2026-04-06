using Aonik.Finance.Contracts.Models.Partners;
using Aonik.Finance.Contracts.Services.Partners;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.Partners;

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
        Summary(s =>
        {
            s.Summary = "Get a partner by ID";
            s.Description = "Retrieves the full details of a partner by its unique identifier.";
            s.Response(200, "Partner retrieved successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "Partner not found");
        });
        Options(x => x.WithTags("Partners"));
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
