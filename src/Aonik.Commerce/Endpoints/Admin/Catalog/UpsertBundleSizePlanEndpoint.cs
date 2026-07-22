using Aonik.Commerce.Contracts.Api.Catalog;
using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Services.Catalog;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Catalog;

/// <summary>Upsert a bundle's size plan — full replace, validating Spec 068's authoring rules
/// (A1–A5). Presets override the formula at their size; everything about the table is data.</summary>
public class UpsertBundleSizePlanEndpoint : Endpoint<UpsertBundleSizePlanRequest, BoxPlanDto>
{
    private readonly IBundleSizePlanService _plans;

    public UpsertBundleSizePlanEndpoint(IBundleSizePlanService plans) => _plans = plans;

    public override void Configure()
    {
        Put("/commerce/admin/products/{productId:guid}/size-plan");
        Policies("AdminWritePolicy");
        Summary(s => s.Summary = "Upsert a bundle product's size plan (presets + formula).");
    }

    public override async Task HandleAsync(UpsertBundleSizePlanRequest req, CancellationToken ct)
    {
        var result = await _plans.UpsertAsync(
            Route<Guid>("productId"),
            new UpsertBundleSizePlanCommand(
                req.MinSize, req.MaxSize, req.BaseSize, req.BasePrice, req.PerSpacePrice, req.Currency,
                (req.Presets ?? []).Select(p => new BundleSizePresetCommand(
                    p.Size, p.Price, p.Badge, p.Blurb, p.SavingAmount, p.SortOrder)).ToList()),
            ct);
        await Send.OkAsync(result, ct);
    }
}
