using Aonik.Api.Contracts.Ai;
using Aonik.Application.Services.Ai;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Ai;

public class GenerateInvoiceInsightEndpoint : EndpointWithoutRequest<InsightResponse>
{
    private readonly IAiInsightsService _aiInsightsService;

    public GenerateInvoiceInsightEndpoint(IAiInsightsService aiInsightsService)
    {
        _aiInsightsService = aiInsightsService;
    }

    public override void Configure()
    {
        Post("/ai/invoices/{id}/insight");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var result = await _aiInsightsService.GenerateInvoiceInsightAsync(id, ct);

        var response = new InsightResponse(
            result.Id,
            result.SubjectType,
            result.SubjectId,
            result.Title,
            result.Summary,
            result.CreatedUtc);

        await Send.OkAsync(response, ct);
    }
}
