using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Services.Promotions;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Promotions;

public sealed class ListDiscountsEndpoint(IDiscountService discounts) : EndpointWithoutRequest<PagedResult<DiscountDto>>
{
    public override void Configure()
    {
        Get("/commerce/admin/discounts");
        Policies("AdminReadPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync(await discounts.ListAsync(new ListDiscountsQuery(
            Query<int?>("page", isRequired: HttpContext.Request.Query.ContainsKey("page")) ?? 1,
            Query<int?>("pageSize", isRequired: HttpContext.Request.Query.ContainsKey("pageSize")) ?? 20,
            Query<string?>("search", isRequired: false),
            Query<bool?>("isActive", isRequired: HttpContext.Request.Query.ContainsKey("isActive"))), ct), ct);
}
