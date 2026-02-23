using Aonik.Ai.Contracts.Models;
using Aonik.Ai.Contracts.Services;
using FastEndpoints;

namespace Aonik.Ai.Endpoints;

internal sealed class GenerateInvoiceInsightEndpoint : EndpointWithoutRequest<InsightResponse>
{
    private readonly IAiInsightsService _aiInsightsService;

    public GenerateInvoiceInsightEndpoint(IAiInsightsService aiInsightsService)
    {
        _aiInsightsService = aiInsightsService;
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
            var result = await _aiInsightsService.GenerateInvoiceInsightAsync(id, ct);
            await Send.OkAsync(result, ct);
        }
        catch (InvalidOperationException)
        {
            await Send.NotFoundAsync(ct);
        }
    }
}
