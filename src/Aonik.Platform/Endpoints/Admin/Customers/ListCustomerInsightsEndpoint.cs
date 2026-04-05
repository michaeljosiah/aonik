using Aonik.Finance.Contracts.Services.PersonalFinance;
using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions.Ai;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Platform.Endpoints.Admin.Customers;

internal sealed class CustomerInsightAiSummaryDetail
{
    public Guid Id { get; set; }
    public string Headline { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public IReadOnlyList<string> KeyObservations { get; set; } = [];
    public IReadOnlyList<string> PositivePatterns { get; set; } = [];
    public IReadOnlyList<string> RiskPatterns { get; set; } = [];
    public IReadOnlyList<string> RecommendedFocusAreas { get; set; } = [];
    public IReadOnlyList<string> ConversationSuggestions { get; set; } = [];
    public IReadOnlyList<string> Caveats { get; set; } = [];
    public string NarrativeVersion { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
}

internal sealed class CustomerInsightSnapshotOverview
{
    public Guid Id { get; set; }
    public DateTime AsOfUtc { get; set; }
    public bool IsPartial { get; set; }
    public string? TopSignalTitle { get; set; }
    public string? TopSignalDescription { get; set; }
    public string? CashflowStressLevel { get; set; }
    public DateTime CreatedUtc { get; set; }
}

internal sealed class ListCustomerInsightsResponse
{
    public CustomerInsightAiSummaryDetail? AiSummary { get; set; }
    public CustomerInsightSnapshotOverview? Snapshot { get; set; }
}

internal sealed class ListCustomerInsightsEndpoint : EndpointWithoutRequest<ListCustomerInsightsResponse>
{
    private readonly PlatformDbContext _dbContext;
    private readonly ICustomerInsightSnapshotReader _snapshotReader;
    private readonly ICustomerInsightAiSummaryReader _summaryReader;

    public ListCustomerInsightsEndpoint(
        PlatformDbContext dbContext,
        ICustomerInsightSnapshotReader snapshotReader,
        ICustomerInsightAiSummaryReader summaryReader)
    {
        _dbContext = dbContext;
        _snapshotReader = snapshotReader;
        _summaryReader = summaryReader;
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
        if (snapshot?.Snapshot is null)
        {
            await Send.OkAsync(new ListCustomerInsightsResponse(), ct);
            return;
        }

        var snapshotOverview = new CustomerInsightSnapshotOverview
        {
            Id = snapshot.Id,
            AsOfUtc = snapshot.AsOfUtc,
            IsPartial = snapshot.Snapshot.Coverage.IsPartial,
            TopSignalTitle = snapshot.Snapshot.Signals.FirstOrDefault()?.Title,
            TopSignalDescription = snapshot.Snapshot.Signals.FirstOrDefault()?.Description,
            CashflowStressLevel = snapshot.Snapshot.Risk.CashflowStressLevel,
            CreatedUtc = snapshot.CreatedAt
        };

        var aiSummary = await _summaryReader.GetCurrentSummaryForSnapshotAsync(snapshot.Id, ct);

        CustomerInsightAiSummaryDetail? aiSummaryDetail = null;
        if (aiSummary?.Summary is not null)
        {
            aiSummaryDetail = new CustomerInsightAiSummaryDetail
            {
                Id = aiSummary.Id,
                Headline = aiSummary.Summary.Headline,
                Summary = aiSummary.Summary.Summary,
                KeyObservations = aiSummary.Summary.KeyObservations,
                PositivePatterns = aiSummary.Summary.PositivePatterns,
                RiskPatterns = aiSummary.Summary.RiskPatterns,
                RecommendedFocusAreas = aiSummary.Summary.RecommendedFocusAreas,
                ConversationSuggestions = aiSummary.Summary.ConversationSuggestions,
                Caveats = aiSummary.Summary.Caveats,
                NarrativeVersion = aiSummary.NarrativeVersion,
                CreatedUtc = aiSummary.CreatedAt
            };
        }

        await Send.OkAsync(new ListCustomerInsightsResponse
        {
            AiSummary = aiSummaryDetail,
            Snapshot = snapshotOverview
        }, ct);
    }
}
