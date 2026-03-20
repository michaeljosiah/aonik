using System.Text.Json;
using System.Text.Json.Serialization;

using Aonik.Finance.Contracts.Services.PersonalFinance;
using Aonik.Finance.Entities.PersonalFinance;
using Aonik.SharedKernel.Abstractions.Ai;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Aonik.Finance.Services.PersonalFinance;

/// <summary>
/// LLM-powered transaction classifier that acts as a fallback when deterministic
/// rules do not match. Uses a single LLM call per classification request.
/// Confidence is capped at 0.7 per the confidence hierarchy:
/// Manual (1.0) → User rule (0.9) → System rule (0.8) → AI (0.7) → Provider (0.55).
/// </summary>
internal sealed class TransactionAiClassifier : ITransactionAiClassifier
{
    private const string PromptName = "transaction_classification";
    private const string PromptVersion = "v1";
    private const string UseCase = "personal_finance_transaction_classification";
    private const string ClassifierVersionTag = "ai_llm_v1";
    private const decimal MaxAiConfidence = 0.7m;
    private const int MaxBatchSize = 50;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly IChatClient _chatClient;
    private readonly IPromptStore _promptStore;
    private readonly IAiRunWriter _aiRunWriter;
    private readonly ILogger<TransactionAiClassifier> _logger;

    public TransactionAiClassifier(
        IChatClient chatClient,
        IPromptStore promptStore,
        IAiRunWriter aiRunWriter,
        ILogger<TransactionAiClassifier> logger)
    {
        _chatClient = chatClient;
        _promptStore = promptStore;
        _aiRunWriter = aiRunWriter;
        _logger = logger;
    }

    public async Task<bool> ClassifyAsync(
        PersonalTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        var results = await ClassifyInternalAsync([transaction], cancellationToken);
        return results > 0;
    }

    public async Task<int> ClassifyBatchAsync(
        IReadOnlyList<PersonalTransaction> transactions,
        CancellationToken cancellationToken = default)
    {
        if (transactions.Count == 0)
        {
            return 0;
        }

        // Process in chunks to avoid exceeding token limits
        var totalClassified = 0;
        for (var i = 0; i < transactions.Count; i += MaxBatchSize)
        {
            var chunk = transactions.Skip(i).Take(MaxBatchSize).ToList();
            totalClassified += await ClassifyInternalAsync(chunk, cancellationToken);
        }

        return totalClassified;
    }

    private async Task<int> ClassifyInternalAsync(
        IReadOnlyList<PersonalTransaction> transactions,
        CancellationToken cancellationToken)
    {
        var transactionInputs = transactions.Select(t => new TransactionInput
        {
            Id = t.Id.ToString(),
            Merchant = t.Merchant,
            Description = t.Description,
            Amount = t.Amount,
            Currency = t.Currency,
            TransactionType = t.TransactionType,
        }).ToList();

        var transactionsJson = JsonSerializer.Serialize(transactionInputs, JsonOptions);

        var systemPrompt = await _promptStore.LoadPromptAsync(
            PromptName, PromptVersion, "system", cancellationToken);

        var userPromptTemplate = await _promptStore.LoadPromptAsync(
            PromptName, PromptVersion, "user", cancellationToken);

        var userPrompt = userPromptTemplate.Replace("{{TRANSACTIONS_JSON}}", transactionsJson);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, userPrompt),
        };

        var inputRefsJson = JsonSerializer.Serialize(
            new { TransactionIds = transactions.Select(t => t.Id).ToList() }, JsonOptions);

        var aiRunId = await _aiRunWriter.StartRunAsync(UseCase, inputRefsJson, cancellationToken);

        try
        {
            var chatResponse = await _chatClient.GetResponseAsync(messages, cancellationToken: cancellationToken);
            var responseText = chatResponse.Text ?? string.Empty;

            var classifications = ParseClassifications(responseText);
            if (classifications.Count == 0)
            {
                _logger.LogWarning(
                    "AI classifier returned no parseable classifications for {Count} transactions. AiRunId: {AiRunId}",
                    transactions.Count, aiRunId);

                await _aiRunWriter.MarkRunCompletedAsync(
                    aiRunId, "no_parseable_results", cancellationToken);
                return 0;
            }

            var classificationLookup = classifications
                .Where(c => !string.IsNullOrWhiteSpace(c.Id))
                .ToDictionary(c => c.Id!, StringComparer.OrdinalIgnoreCase);

            var classifiedCount = 0;
            foreach (var transaction in transactions)
            {
                if (!classificationLookup.TryGetValue(transaction.Id.ToString(), out var classification))
                {
                    continue;
                }

                if (!ApplyClassification(transaction, classification, aiRunId))
                {
                    continue;
                }

                classifiedCount++;
            }

            _logger.LogInformation(
                "AI classifier classified {Classified}/{Total} transactions. AiRunId: {AiRunId}",
                classifiedCount, transactions.Count, aiRunId);

            await _aiRunWriter.MarkRunCompletedAsync(
                aiRunId,
                $"classified:{classifiedCount}/{transactions.Count}",
                cancellationToken);

            return classifiedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "AI classification failed for {Count} transactions. AiRunId: {AiRunId}",
                transactions.Count, aiRunId);

            await TryMarkRunFailedAsync(aiRunId, ex.Message);
            return 0;
        }
    }

    private static bool ApplyClassification(
        PersonalTransaction transaction,
        ClassificationResult classification,
        Guid aiRunId)
    {
        var category = classification.Category?.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(category)
            || string.Equals(category, "uncategorized", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!TransactionCategoryReference.IsValidCategory(category))
        {
            return false;
        }

        var confidence = Math.Min(Math.Max(classification.Confidence, 0m), MaxAiConfidence);

        transaction.Category = category;
        var subCategory = string.IsNullOrWhiteSpace(classification.SubCategory)
            ? null
            : classification.SubCategory.Trim().ToLowerInvariant();
        transaction.SubCategory = subCategory != null
            && TransactionCategoryReference.IsValidSubCategory(category, subCategory)
            ? subCategory
            : null;
        transaction.Confidence = confidence;
        transaction.CategorisedBy = "ai";
        transaction.ClassificationMethod = "ai_llm";
        transaction.ClassifierVersion = ClassifierVersionTag;
        transaction.AiRunId = aiRunId;
        transaction.TransactionType = TransactionCategoryReference.ResolveTransactionType(
            transaction.Category, transaction.Amount);

        return true;
    }

    private List<ClassificationResult> ParseClassifications(string responseText)
    {
        try
        {
            // Strip markdown code fences if present
            var trimmed = responseText.Trim();
            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                var firstNewline = trimmed.IndexOf('\n');
                if (firstNewline >= 0)
                {
                    trimmed = trimmed[(firstNewline + 1)..];
                }

                if (trimmed.EndsWith("```", StringComparison.Ordinal))
                {
                    trimmed = trimmed[..^3];
                }

                trimmed = trimmed.Trim();
            }

            // Try parsing as array first
            if (trimmed.StartsWith('['))
            {
                return JsonSerializer.Deserialize<List<ClassificationResult>>(trimmed, JsonOptions)
                    ?? [];
            }

            // Try parsing as single object
            if (trimmed.StartsWith('{'))
            {
                var single = JsonSerializer.Deserialize<ClassificationResult>(trimmed, JsonOptions);
                return single != null ? [single] : [];
            }

            return [];
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse AI classification response: {Response}",
                responseText.Length > 500 ? responseText[..500] : responseText);
            return [];
        }
    }

    private async Task TryMarkRunFailedAsync(Guid aiRunId, string failureReason)
    {
        try
        {
            await _aiRunWriter.MarkRunFailedAsync(aiRunId, failureReason, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to mark AI run {AiRunId} as failed", aiRunId);
        }
    }

    /// <summary>
    /// Input DTO sent to the LLM (no PII — uses IDs, merchant names, amounts only).
    /// </summary>
    private sealed class TransactionInput
    {
        public string Id { get; set; } = string.Empty;
        public string? Merchant { get; set; }
        public string? Description { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = string.Empty;
        public string TransactionType { get; set; } = string.Empty;
    }

    /// <summary>
    /// Output DTO expected from the LLM.
    /// </summary>
    private sealed class ClassificationResult
    {
        public string? Id { get; set; }
        public string? Category { get; set; }
        public string? SubCategory { get; set; }
        public decimal Confidence { get; set; }
    }
}
