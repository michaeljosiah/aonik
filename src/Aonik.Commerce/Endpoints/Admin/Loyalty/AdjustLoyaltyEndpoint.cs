using Aonik.Authorization;
using Aonik.Commerce.Contracts.Api.Loyalty;
using Aonik.SharedKernel.Abstractions.Loyalty;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Loyalty;

public sealed class AdjustLoyaltyEndpoint(ILoyaltyService loyalty) : Endpoint<AdjustLoyaltyRequest, LoyaltyOperationRef>
{
    public override void Configure()
    {
        Post("/commerce/admin/loyalty/adjustments");
        Policies("AdminWritePolicy");
        this.RequiresPermission("Ledger.Write");
    }

    public override async Task HandleAsync(AdjustLoyaltyRequest req, CancellationToken ct)
    {
        HttpContext.Response.Headers.CacheControl = "no-store";
        await Send.OkAsync(await loyalty.AdjustAsync(new(req.PartyId, req.AdjustmentId, req.Points, req.Reason), ct), ct);
    }
}
