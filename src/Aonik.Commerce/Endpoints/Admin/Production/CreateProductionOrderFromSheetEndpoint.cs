using Aonik.Commerce.Contracts.Api.Production;
using Aonik.Commerce.Contracts.Models.Production;
using Aonik.Commerce.Services.Production;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Production;

public class CreateProductionOrderFromSheetEndpoint : Endpoint<CreateProductionOrderFromSheetRequest, ProductionOrderFromSheetDto>
{
    private readonly IProductionOrderService _productionOrders;

    public CreateProductionOrderFromSheetEndpoint(IProductionOrderService productionOrders) => _productionOrders = productionOrders;

    public override void Configure()
    {
        Post("/commerce/admin/production-orders/from-sheet");
        Policies("AdminUserWritePolicy");
        Summary(s => s.Summary =
            "Seed a Planned production run from the Spec 055 production sheet for a UTC window " +
            "(half-open [fromUtc, toUtc)): one line per demanded variant with an active recipe. " +
            "Demanded variants without a recipe are skipped and reported in SkippedVariants — never silently dropped.");
    }

    public override async Task HandleAsync(CreateProductionOrderFromSheetRequest req, CancellationToken ct)
    {
        var result = await _productionOrders.CreateFromProductionSheetAsync(
            new CreateFromProductionSheetCommand(req.FromUtc, req.ToUtc, req.PlannedFor, req.Notes), ct);
        await Send.OkAsync(result, ct);
    }
}
