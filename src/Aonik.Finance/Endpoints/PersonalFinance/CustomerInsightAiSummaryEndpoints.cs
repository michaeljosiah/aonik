using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Ai;
using FastEndpoints;

namespace Aonik.Finance.Endpoints.PersonalFinance;

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
