using Aonik.Finance.Contracts.Services.PersonalFinance;
using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions.Ai;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Platform.Endpoints.Admin.Customers;

internal sealed class CustomerInsightItem
{
    public Guid Id { get; set; }
    public string SubjectType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
}

internal sealed class ListCustomerInsightsResponse
{
    public IReadOnlyList<CustomerInsightItem> Items { get; set; } = [];
}

internal sealed class ListCustomerInsightsEndpoint : EndpointWithoutRequest<ListCustomerInsightsResponse>
{
    private readonly PlatformDbContext _dbContext;
    private readonly ICustomerInsightSnapshotReader _snapshotReader;
    private readonly ICustomerInsightAiSummaryReader _summaryReader;
    private readonly IInsightReader _insightReader;

    public ListCustomerInsightsEndpoint(
        PlatformDbContext dbContext,
        ICustomerInsightSnapshotReader snapshotReader,
        ICustomerInsightAiSummaryReader summaryReader,
        IInsightReader insightReader)
    {
        _dbContext = dbContext;
        _snapshotReader = snapshotReader;
        _summaryReader = summaryReader;
        _insightReader = insightReader;
    }

    public override void Configure()
    {
        Get("/admin/customers/{partyId}/insights");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var partyId = Route<Guid>("partyId");

        var userId = await _dbContext.UserParties
            .AsNoTracking()
            .Where(up => up.PartyId == partyId)
            .Select(up => up.UserId)
            .FirstOrDefaultAsync(ct);

        if (userId == Guid.Empty)
        {
            await Send.OkAsync(new ListCustomerInsightsResponse(), ct);
            return;
        }

        var snapshot = await _snapshotReader.GetCurrentSnapshotAsync(userId, ct);
        if (snapshot?.Snapshot is not null)
        {
            var aiSummary = await _summaryReader.GetCurrentSummaryForSnapshotAsync(snapshot.Id, ct);
            var canonicalItems = BuildCanonicalItems(snapshot, aiSummary);
            await Send.OkAsync(new ListCustomerInsightsResponse { Items = canonicalItems }, ct);
            return;
        }

        var insights = await _insightReader.ListBySubjectAsync("UserBehaviour", userId, ct);

        var items = insights.Select(i => new CustomerInsightItem
        {
            Id = i.Id,
            SubjectType = i.SubjectType,
            Title = i.Title,
            Summary = i.Summary,
            CreatedUtc = i.CreatedUtc
        }).ToList();

        await Send.OkAsync(new ListCustomerInsightsResponse { Items = items }, ct);
    }

    private static IReadOnlyList<CustomerInsightItem> BuildCanonicalItems(
        Aonik.Finance.Contracts.Models.PersonalFinance.CustomerInsightSnapshotResponse snapshot,
        CustomerInsightAiSummaryResponse? aiSummary)
    {
        if (aiSummary?.Summary is not null)
        {
            return
            [
                new CustomerInsightItem
                {
                    Id = aiSummary.Id,
                    SubjectType = "CustomerInsightAiSummary",
                    Title = aiSummary.Summary.Headline,
                    Summary = BuildAiSummary(aiSummary),
                    CreatedUtc = aiSummary.CreatedAt
                }
            ];
        }

        return
        [
            new CustomerInsightItem
            {
                Id = snapshot.Id,
                SubjectType = "CustomerInsightSnapshot",
                Title = snapshot.Snapshot!.Signals.FirstOrDefault()?.Title ?? "Customer insight snapshot available",
                Summary = BuildSnapshotFallbackSummary(snapshot.Snapshot),
                CreatedUtc = snapshot.CreatedAt
            }
        ];
    }

    private static string BuildAiSummary(CustomerInsightAiSummaryResponse summary)
    {
        var parts = new List<string> { summary.Summary!.Summary };

        if (summary.Summary.RecommendedFocusAreas.Count > 0)
        {
            parts.Add($"Focus: {string.Join("; ", summary.Summary.RecommendedFocusAreas.Take(2))}.");
        }

        if (summary.Summary.Caveats.Count > 0)
        {
            parts.Add(string.Join(" ", summary.Summary.Caveats.Take(1)));
        }

        return string.Join(" ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private static string BuildSnapshotFallbackSummary(
        Aonik.Finance.Contracts.Models.PersonalFinance.CustomerInsightSnapshotDocument snapshot)
    {
        var parts = new List<string>();

        var topCategory = snapshot.Metrics.Categories.TopCategoriesByAmount.FirstOrDefault();
        if (topCategory is not null)
        {
            parts.Add($"Top spend category is {topCategory.Category} at {topCategory.Amount} {topCategory.Currency} ({topCategory.ShareOfSpend}% of spend).");
        }

        var topSignal = snapshot.Signals.FirstOrDefault();
        if (topSignal is not null)
        {
            parts.Add(topSignal.Description);
        }

        if (!string.Equals(snapshot.Risk.CashflowStressLevel, "Low", StringComparison.OrdinalIgnoreCase))
        {
            parts.Add($"Cashflow stress level: {snapshot.Risk.CashflowStressLevel}.");
        }

        if (snapshot.Coverage.IsPartial)
        {
            parts.Add("This deterministic snapshot is partial.");
        }

        return string.Join(" ", parts);
    }
}
