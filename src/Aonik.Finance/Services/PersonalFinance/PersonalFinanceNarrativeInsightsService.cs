using System.Text.Json;

using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using Aonik.SharedKernel.Abstractions.Ai;
using Microsoft.Extensions.AI;

namespace Aonik.Finance.Services.PersonalFinance;

internal sealed class PersonalFinanceNarrativeInsightsService : IPersonalFinanceNarrativeInsightsService
{
    private readonly IPersonalFinanceInsightsService _insightsService;
    private readonly IPromptStore _promptStore;
    private readonly IChatClient _chatClient;
    private readonly IInsightWriter _insightWriter;
    private readonly IAiRunWriter _aiRunWriter;

    public PersonalFinanceNarrativeInsightsService(
        IPersonalFinanceInsightsService insightsService,
        IPromptStore promptStore,
        IChatClient chatClient,
        IInsightWriter insightWriter,
        IAiRunWriter aiRunWriter)
    {
        _insightsService = insightsService;
        _promptStore = promptStore;
        _chatClient = chatClient;
        _insightWriter = insightWriter;
        _aiRunWriter = aiRunWriter;
    }

    public async Task<PersonalSpendingNarrativeInsightResponse> GenerateSpendingNarrativeAsync(
        GeneratePersonalSpendingNarrativeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.PeriodStart == default || request.PeriodEnd == default)
        {
            throw new ArgumentException("PeriodStart and PeriodEnd are required.");
        }

        if (request.PeriodEnd < request.PeriodStart)
        {
            throw new ArgumentException("PeriodEnd must be greater than or equal to PeriodStart.");
        }

        var summary = await _insightsService.GetSpendingSummaryAsync(
            request.PeriodStart,
            request.PeriodEnd,
            request.PersonalAccountId,
            cancellationToken);

        var topCategories = await _insightsService.GetCategoryBreakdownAsync(
            request.PeriodStart,
            request.PeriodEnd,
            request.PersonalAccountId,
            cancellationToken);

        var topMerchants = await _insightsService.GetMerchantBreakdownAsync(
            request.PeriodStart,
            request.PeriodEnd,
            request.PersonalAccountId,
            5,
            cancellationToken);

        var systemPrompt = await _promptStore.LoadPromptAsync(
            "personal_spending_insight",
            "v1",
            "system",
            cancellationToken);

        var userPromptTemplate = await _promptStore.LoadPromptAsync(
            "personal_spending_insight",
            "v1",
            "user",
            cancellationToken);

        var insightData = new
        {
            summary,
            topCategories,
            topMerchants
        };

        var insightDataJson = JsonSerializer.Serialize(insightData, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        var userPrompt = userPromptTemplate.Replace("{{SPENDING_DATA}}", insightDataJson);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, userPrompt)
        };

        var chatResponse = await _chatClient.GetResponseAsync(messages, cancellationToken: cancellationToken);
        var narrative = chatResponse.Text ?? string.Empty;

        var inputRefsJson = JsonSerializer.Serialize(new
        {
            request.PeriodStart,
            request.PeriodEnd,
            request.PersonalAccountId
        });

        var aiRunId = await _aiRunWriter.SaveRunAsync(
            "personal_finance_spending_narrative",
            inputRefsJson,
            "Completed",
            cancellationToken);

        var subjectId = Guid.NewGuid();
        var insight = await _insightWriter.SaveInsightAsync(
            "PersonalSpendPeriod",
            subjectId,
            "Spending Narrative Insight",
            narrative,
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
}
