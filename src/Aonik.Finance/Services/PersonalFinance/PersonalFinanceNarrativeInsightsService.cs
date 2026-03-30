using System.Text.Json;

using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using Aonik.SharedKernel.Abstractions.Ai;
using Microsoft.Extensions.AI;

namespace Aonik.Finance.Services.PersonalFinance;

internal sealed class PersonalFinanceNarrativeInsightsService : IPersonalFinanceNarrativeInsightsService
{
    private readonly IPersonalFinanceInsightsService _insightsService;
    private readonly IAiTaskProfileResolver _profileResolver;
    private readonly IChatClient _chatClient;
    private readonly IInsightWriter _insightWriter;
    private readonly IAiRunWriter _aiRunWriter;

    private const string UseCase = "personal_finance_spending_narrative";
    private const string PromptName = "personal_spending_insight";

    public PersonalFinanceNarrativeInsightsService(
        IPersonalFinanceInsightsService insightsService,
        IAiTaskProfileResolver profileResolver,
        IChatClient chatClient,
        IInsightWriter insightWriter,
        IAiRunWriter aiRunWriter)
    {
        _insightsService = insightsService;
        _profileResolver = profileResolver;
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

        var profile = await _profileResolver.ResolveAsync(UseCase, PromptName, cancellationToken: cancellationToken);

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

        var userPrompt = (profile.UserPromptTemplate ?? "{{SPENDING_DATA}}")
            .Replace("{{SPENDING_DATA}}", insightDataJson);

        var messages = new List<ChatMessage>();
        if (!string.IsNullOrEmpty(profile.SystemPrompt))
            messages.Add(new ChatMessage(ChatRole.System, profile.SystemPrompt));
        messages.Add(new ChatMessage(ChatRole.User, userPrompt));

        var inputRefsJson = JsonSerializer.Serialize(new
        {
            request.PeriodStart,
            request.PeriodEnd,
            request.PersonalAccountId
        });

        var aiRunId = await _aiRunWriter.StartRunAsync(
            UseCase,
            inputRefsJson,
            cancellationToken);

        try
        {
            var chatOptions = profile.ModelId is not null ? new ChatOptions { ModelId = profile.ModelId } : null;
            var chatResponse = await _chatClient.GetResponseAsync(messages, options: chatOptions, cancellationToken: cancellationToken);
            var narrative = chatResponse.Text ?? string.Empty;

            var subjectId = Guid.NewGuid();
            var insight = await _insightWriter.SaveInsightAsync(
                "PersonalSpendPeriod",
                subjectId,
                "Spending Narrative Insight",
                narrative,
                cancellationToken);

            await _aiRunWriter.MarkRunCompletedAsync(
                aiRunId,
                $"insight:{insight.Id}",
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
        catch (Exception ex)
        {
            await TryMarkRunFailedAsync(aiRunId, ex.Message);
            throw;
        }
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
