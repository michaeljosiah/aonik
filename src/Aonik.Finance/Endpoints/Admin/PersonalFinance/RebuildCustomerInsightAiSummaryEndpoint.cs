using Aonik.SharedKernel.Abstractions.Ai;
using FastEndpoints;

namespace Aonik.Finance.Endpoints.Admin.PersonalFinance;

internal sealed class RebuildCustomerInsightAiSummaryRequest
{
    public Guid SnapshotId { get; set; }
}

internal sealed class RebuildCustomerInsightAiSummaryEndpoint : Endpoint<RebuildCustomerInsightAiSummaryRequest, CustomerInsightAiSummaryResponse>
{
    private readonly ICustomerInsightAiSummaryService _summaryService;

    public RebuildCustomerInsightAiSummaryEndpoint(ICustomerInsightAiSummaryService summaryService)
    {
        _summaryService = summaryService;
    }

    public override void Configure()
    {
        Post("/admin/personal-finance/customer-insights/rebuild-ai-summary/{SnapshotId}");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(RebuildCustomerInsightAiSummaryRequest req, CancellationToken ct)
    {
        var snapshotId = req.SnapshotId == Guid.Empty ? Route<Guid>("SnapshotId") : req.SnapshotId;
        var summary = await _summaryService.GenerateCurrentSummaryAsync(snapshotId, ct);
        await Send.OkAsync(summary, ct);
    }
}
