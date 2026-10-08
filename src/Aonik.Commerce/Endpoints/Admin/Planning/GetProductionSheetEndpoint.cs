using Aonik.Commerce.Contracts.Models.Production;
using Aonik.Commerce.Services.Catalog;
using Aonik.Commerce.Services.Production;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Planning;

public class GetProductionSheetEndpoint : EndpointWithoutRequest<ProductionSheetDto>
{
    private readonly IProductionPlanningService _planning;

    public GetProductionSheetEndpoint(IProductionPlanningService planning) => _planning = planning;

    public override void Configure()
    {
        Get("/commerce/admin/planning/production-sheet");
        Policies("AdminUserPolicy");
        Summary(s => s.Summary =
            "The aggregated production sheet: per-variant portion demand from committed product-purchase " +
            "orders created in ?fromUtc=&toUtc= (UTC, half-open [from, to)), or scheduled for " +
            "?deliveryDate= (calendar-local date). Supply exactly one selector. Bundle lines are expanded " +
            "into their chosen component variants.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var fromUtc = Query<DateTime?>("fromUtc", isRequired: HttpContext.Request.Query.ContainsKey("fromUtc"));
        var toUtc = Query<DateTime?>("toUtc", isRequired: HttpContext.Request.Query.ContainsKey("toUtc"));
        var deliveryDate = Query<DateOnly?>("deliveryDate", isRequired: HttpContext.Request.Query.ContainsKey("deliveryDate"));
        if (deliveryDate.HasValue ? fromUtc.HasValue || toUtc.HasValue : !fromUtc.HasValue || !toUtc.HasValue)
        {
            throw new StorefrontValidationException("Supply either deliveryDate or both fromUtc and toUtc.");
        }
        var result = deliveryDate is { } date
            ? await _planning.GetProductionSheetForDeliveryDateAsync(date, ct)
            : await _planning.GetProductionSheetAsync(new ProductionWindow(fromUtc!.Value, toUtc!.Value), ct);
        await Send.OkAsync(result, ct);
    }
}
