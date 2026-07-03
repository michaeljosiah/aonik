using Aonik.Commerce.Contracts.Models.Production;
using Aonik.Commerce.Contracts.Models.Reporting;
using Aonik.Commerce.Services.Reporting;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Reporting;

public class GetMarginReportEndpoint : EndpointWithoutRequest<MarginReportDto>
{
    private readonly IMarginReportService _margins;

    public GetMarginReportEndpoint(IMarginReportService margins) => _margins = margins;

    public override void Configure()
    {
        Get("/commerce/admin/reports/margin");
        Policies("AdminUserPolicy");
        Summary(s => s.Summary =
            "The margin & profit report: per-variant and aggregate revenue vs standard-cost COGS vs " +
            "target margin, over payment-completed product-purchase orders created in ?fromUtc=&toUtc= " +
            "(UTC, half-open [from, to)) in ?currency=. Revenue is the discounted charge-summary total " +
            "(tax excluded); bundle lines are expanded into their chosen components; a variant with no " +
            "recipe or missing ingredient cost is surfaced as COGS-unknown and excluded from the " +
            "aggregate margin — never counted as zero cost.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var fromUtc = Query<DateTime>("fromUtc");
        var toUtc = Query<DateTime>("toUtc");
        var currency = Query<string>("currency");
        var result = await _margins.GetMarginReportAsync(new ProductionWindow(fromUtc, toUtc), currency!, ct);
        await Send.OkAsync(result, ct);
    }
}
