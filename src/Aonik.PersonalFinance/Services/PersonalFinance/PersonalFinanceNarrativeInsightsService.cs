using System.Text.Json;

using Aonik.PersonalFinance.Contracts.Models;
using Aonik.PersonalFinance.Contracts.Services;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Ai;

namespace Aonik.PersonalFinance.Services;

internal sealed class PersonalFinanceNarrativeInsightsService : IPersonalFinanceNarrativeInsightsService
{
    private const string UseCase = "personal_finance_spending_narrative";

    private readonly ICustomerInsightSnapshotReader _snapshotReader;
    private readonly ICustomerInsightSnapshotService _snapshotService;
    private readonly ICustomerInsightAiSummaryReader _summaryReader;
    private readonly IInsightWriter _insightWriter;
    private readonly IAiRunWriter _aiRunWriter;
    private readonly ICurrentUserProvider _currentUserProvider;

    public PersonalFinanceNarrativeInsightsService(
        ICustomerInsightSnapshotReader snapshotReader,
        ICustomerInsightSnapshotService snapshotService,
        ICustomerInsightAiSummaryReader summaryReader,
        IInsightWriter insightWriter,
        IAiRunWriter aiRunWriter,
        ICurrentUserProvider currentUserProvider)
    {
        _snapshotReader = snapshotReader;
        _snapshotService = snapshotService;
        _summaryReader = summaryReader;
        _insightWriter = insightWriter;
        _aiRunWriter = aiRunWriter;
        _currentUserProvider = currentUserProvider;
    }

    public async Task<PersonalSpendingNarrativeInsightResponse> GenerateSpendingNarrativeAsync(
        GeneratePersonalSpendingNarrativeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.PeriodEnd < request.PeriodStart)
        {
            throw new ArgumentException("PeriodEnd must be greater than or equal to PeriodStart.");
        }

        var userId = _currentUserProvider.GetCurrentUserId()
            ?? throw new InvalidOperationException("Authenticated user is required.");

        var snapshot = await _snapshotReader.GetCurrentSnapshotAsync(userId, cancellationToken)
            ?? await _snapshotService.GenerateCurrentSnapshotAsync(userId, cancellationToken);

        if (snapshot.Snapshot is null)
        {
            throw new InvalidOperationException("Current customer insight snapshot is not available.");
        }

        var currentSummary = await _summaryReader.GetCurrentSummaryForSnapshotAsync(snapshot.Id, cancellationToken);

        string title;
        string summaryText;
        Guid aiRunId;
        string narrativeSource;
        Guid? customerInsightAiSummaryId = null;

        if (currentSummary?.Summary is not null)
        {
            title = currentSummary.Summary.Headline;
            summaryText = currentSummary.Summary.Summary;
            aiRunId = currentSummary.AiRunId;
            narrativeSource = "customer_insight_ai_summary";
            customerInsightAiSummaryId = currentSummary.Id;
        }
        else
        {
            aiRunId = await _aiRunWriter.StartRunAsync(
                UseCase,
                JsonSerializer.Serialize(new
                {
                    snapshot.Id,
                    snapshot.UserId,
                    snapshot.AsOfUtc,
                    snapshot.WindowStartUtc,
                    snapshot.WindowEndUtc
                }),
                cancellationToken);

            try
            {
                title = "Customer insight narrative";
                summaryText = BuildDeterministicNarrative(snapshot.Snapshot);
                narrativeSource = "customer_insight_snapshot";

                await _aiRunWriter.MarkRunCompletedAsync(
                    aiRunId,
                    $"customer-insight-snapshot:{snapshot.Id}",
                    cancellationToken);
            }
            catch (Exception ex)
            {
                await TryMarkRunFailedAsync(aiRunId, ex.Message);
                throw;
            }
        }

        var metadataJson = JsonSerializer.Serialize(new
        {
            source = narrativeSource,
            customerInsightSnapshotId = snapshot.Id,
            customerInsightAiSummaryId,
            snapshot.AsOfUtc,
            snapshot.WindowStartUtc,
            snapshot.WindowEndUtc,
            request.PeriodStart,
            request.PeriodEnd,
            request.PersonalAccountId
        });

        var insight = await _insightWriter.SaveInsightAsync(
            "CustomerInsightSnapshot",
            snapshot.Id,
            title,
            summaryText,
            metadataJson,
            userId,
            snapshot.AsOfUtc.AddDays(30),
            cancellationToken);

        return new PersonalSpendingNarrativeInsightResponse(
            insight.Id,
            aiRunId,
            insight.SubjectType,
            insight.SubjectId,
            insight.Title,
            insight.Summary,
            insight.CreatedUtc);
    }

    private static string BuildDeterministicNarrative(CustomerInsightSnapshotDocument snapshot)
    {
        var topCategory = snapshot.Metrics.Categories.TopCategoriesByAmount.FirstOrDefault();
        var topMerchant = snapshot.Metrics.Merchants.TopMerchantsByAmount.FirstOrDefault();
        var topSignal = snapshot.Signals.FirstOrDefault();
        var obligations = snapshot.Metrics.Obligations.TotalUpcomingByCurrency
            .Select(x => $"{x.Amount} {x.Currency}")
            .ToList();

        var summaryParts = new List<string>();

        if (topCategory is not null)
        {
            summaryParts.Add($"Top spend category is {topCategory.Category} at {topCategory.Amount} {topCategory.Currency} ({topCategory.ShareOfSpend}% of spend).");
        }

        if (topMerchant is not null)
        {
            summaryParts.Add($"Top merchant concentration is {topMerchant.Merchant} at {topMerchant.Amount} {topMerchant.Currency}.");
        }

        if (obligations.Count > 0)
        {
            summaryParts.Add($"Upcoming obligations total {string.Join(", ", obligations)}.");
        }

        if (topSignal is not null)
        {
            summaryParts.Add($"Key behavioural signal: {topSignal.Title}. {topSignal.Description}");
        }

        if (snapshot.Coverage.IsPartial)
        {
            summaryParts.Add("This narrative is based on a partial deterministic snapshot.");
        }

        return string.Join(" ", summaryParts);
    }

    private async Task TryMarkRunFailedAsync(Guid aiRunId, string failureReason)
    {
        try
        {
            await _aiRunWriter.MarkRunFailedAsync(aiRunId, failureReason, CancellationToken.None);
        }
        catch
        {
        }
    }
}
