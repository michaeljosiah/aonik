using Aonik.Finance.Contracts.Services.Ai;
using Aonik.SharedKernel.Abstractions.Ai;
using FastEndpoints;

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
