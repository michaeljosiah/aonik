using Aonik.Finance.Contracts.Services.Ai;
using Aonik.SharedKernel.Abstractions.Ai;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.Ai;

internal sealed class GenerateInvoiceInsightEndpoint : EndpointWithoutRequest<InsightResponse>
{
    private readonly IFinanceInsightsService _financeInsightsService;

    public GenerateInvoiceInsightEndpoint(IFinanceInsightsService financeInsightsService)
    {
        _financeInsightsService = financeInsightsService;
    }

    public override void Configure()
    {
        Post("/ai/invoices/{id}/insight");
        Policies("AdminUserPolicy");
        Summary(s =>
        {
            s.Summary = "Generate AI insight for an invoice";
            s.Description = "Uses AI to generate an analytical insight for a specific invoice, highlighting key observations.";
            s.Response(200, "Insight generated successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "Invoice not found");
        });
        Options(x => x.WithTags("Billing"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");

        try
        {
            var result = await _financeInsightsService.GenerateInvoiceInsightAsync(id, ct);
            await Send.OkAsync(result, ct);
        }
        catch (InvalidOperationException)
        {
            await Send.NotFoundAsync(ct);
        }
    }
}
