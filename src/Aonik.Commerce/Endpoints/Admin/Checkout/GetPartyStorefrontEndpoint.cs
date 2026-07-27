using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Services.Checkout;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Checkout;

/// <summary>GET /commerce/admin/parties/{partyId}/storefront — Spec 081's
/// Commerce tab: the party's storefront summary through the same party-scoped
/// queries the customer's own account uses; <c>adopted</c> is the recorded fact
/// only (no invented timestamps).</summary>
public class GetPartyStorefrontEndpoint : EndpointWithoutRequest<AdminPartyStorefrontDto>
{
    private readonly IAdminStorefrontService _admin;

    public GetPartyStorefrontEndpoint(IAdminStorefrontService admin) => _admin = admin;

    public override void Configure()
    {
        Get("/commerce/admin/parties/{partyId:guid}/storefront");
        Policies("AdminUserPolicy");
        Summary(s => s.Summary = "A party's storefront summary: order history, active box cart, and the recorded adoption fact.");
    }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync(await _admin.GetPartyStorefrontAsync(Route<Guid>("partyId"), ct), ct);
}
