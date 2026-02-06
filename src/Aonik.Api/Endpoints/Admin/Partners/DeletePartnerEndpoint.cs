using Aonik.Application.Services.Partners;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Admin.Partners;

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
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var partnerId = Route<Guid>("partnerId");
        await _partnerAdminService.DeletePartnerAsync(partnerId, ct);
        await Send.NoContentAsync(ct);
    }
}
