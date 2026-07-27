using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Services.Catalog;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Catalog;

/// <summary>GET /commerce/admin/products/{productId}/size-plan — Spec 076: the
/// admin plan read by PRODUCT ID, independent of product status, so a draft
/// bundle's hidden plan can never be mistaken for a missing one.</summary>
public class GetAdminSizePlanEndpoint : EndpointWithoutRequest<BoxPlanDto>
{
    private readonly IBundleSizePlanService _plans;

    public GetAdminSizePlanEndpoint(IBundleSizePlanService plans) => _plans = plans;

    public override void Configure()
    {
        Get("/commerce/admin/products/{productId:guid}/size-plan");
        Policies("AdminUserPolicy");
        Summary(s => s.Summary = "The bundle's size plan by product id (any product status). 404 = no plan authored.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var plan = await _plans.GetForProductAsync(Route<Guid>("productId"), ct);
        if (plan is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        await Send.OkAsync(plan, ct);
    }
}
