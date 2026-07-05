using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Ai;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.PersonalFinance.Endpoints;

internal sealed class GetCustomerInsightAiSummaryRequest
{
    public Guid SnapshotId { get; set; }
}

internal sealed class GetCustomerInsightAiSummaryEndpoint : Endpoint<GetCustomerInsightAiSummaryRequest, CustomerInsightAiSummaryResponse>
{
    private readonly ICustomerInsightAiSummaryReader _summaryReader;
    private readonly ICurrentUserProvider _currentUserProvider;

    public GetCustomerInsightAiSummaryEndpoint(
        ICustomerInsightAiSummaryReader summaryReader,
        ICurrentUserProvider currentUserProvider)
    {
        _summaryReader = summaryReader;
        _currentUserProvider = currentUserProvider;
    }

    public override void Configure()
    {
        Get("/personal-finance/customer-insights/{SnapshotId}/ai-summary");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Get AI summary for a customer insight";
            s.Description = "Returns the AI-generated natural-language summary for a specific customer insight snapshot, including key observations and recommendations.";
            s.Response(200, "AI summary returned successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "Snapshot or AI summary not found");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(GetCustomerInsightAiSummaryRequest req, CancellationToken ct)
    {
        var userId = _currentUserProvider.GetCurrentUserId()
            ?? throw new InvalidOperationException("Authenticated user is required.");

        var summary = await _summaryReader.GetCurrentSummaryForSnapshotAsync(req.SnapshotId, ct);
        if (summary is null || summary.UserId != userId)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(summary, ct);
    }
}
