using Aonik.SharedKernel.Abstractions.Ai;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

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
        Summary(s =>
        {
            s.Summary = "Rebuild customer insight AI summary";
            s.Description = "Regenerates the AI-powered narrative summary for an existing customer insight snapshot.";
            s.Response(200, "AI summary rebuilt successfully");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(RebuildCustomerInsightAiSummaryRequest req, CancellationToken ct)
    {
        var snapshotId = req.SnapshotId == Guid.Empty ? Route<Guid>("SnapshotId") : req.SnapshotId;
        var summary = await _summaryService.GenerateCurrentSummaryAsync(snapshotId, ct);
        await Send.OkAsync(summary, ct);
    }
}
