using Aonik.Commerce.Contracts.Api.Reporting;
using Aonik.Commerce.Contracts.Models.Reporting;
using Aonik.Commerce.Services.Reporting;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Reporting;

public class SetTargetMarginEndpoint : Endpoint<SetTargetMarginRequest, TargetMarginDto>
{
    private readonly IMarginReportService _margins;

    public SetTargetMarginEndpoint(IMarginReportService margins) => _margins = margins;

    public override void Configure()
    {
        Put("/commerce/admin/products/{productId:guid}/target-margin");
        Policies("AdminWritePolicy");
        Summary(s => s.Summary =
            "Set (or clear, with a null percentage) the product's target gross-margin percentage " +
            "(0–100) that the margin report flags achieved margin against.");
    }

    public override async Task HandleAsync(SetTargetMarginRequest req, CancellationToken ct)
    {
        var productId = Route<Guid>("productId");
        var result = await _margins.SetTargetMarginAsync(productId, req.TargetMarginPct, ct);
        await Send.OkAsync(result, ct);
    }
}
