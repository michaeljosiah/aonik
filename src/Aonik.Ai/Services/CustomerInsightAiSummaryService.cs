using System.Text.Json;
using System.Text.Json.Serialization;

using Aonik.Ai.Entities;
using Aonik.Ai.Persistence;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Ai;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Aonik.Ai.Services;

internal sealed class CustomerInsightAiSummaryService : ICustomerInsightAiSummaryService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly AiDbContext _dbContext;
    private readonly ICustomerInsightSnapshotReader _snapshotReader;
    private readonly ICustomerInsightAiSummaryReader _summaryReader;
    private readonly IAiTaskProfileResolver _profileResolver;
    private readonly IChatClient _chatClient;
    private readonly IAiRunWriter _aiRunWriter;
    private readonly IClock _clock;
    private readonly ILogger<CustomerInsightAiSummaryService> _logger;

    public CustomerInsightAiSummaryService(
        AiDbContext dbContext,
        ICustomerInsightSnapshotReader snapshotReader,
        ICustomerInsightAiSummaryReader summaryReader,
        IAiTaskProfileResolver profileResolver,
        IChatClient chatClient,
        IAiRunWriter aiRunWriter,
        IClock clock,
        ILogger<CustomerInsightAiSummaryService> logger)
    {
        _dbContext = dbContext;
        _snapshotReader = snapshotReader;
        _summaryReader = summaryReader;
        _profileResolver = profileResolver;
        _chatClient = chatClient;
        _aiRunWriter = aiRunWriter;
        _clock = clock;
        _logger = logger;
    }

    public async Task<CustomerInsightAiSummaryResponse> GenerateCurrentSummaryAsync(
        Guid customerInsightSnapshotId,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await _snapshotReader.GetSnapshotAsync(customerInsightSnapshotId, cancellationToken)
            ?? throw new InvalidOperationException($"Customer insight snapshot {customerInsightSnapshotId} was not found.");

        if (snapshot.Snapshot is null)
        {
            throw new InvalidOperationException($"Customer insight snapshot {customerInsightSnapshotId} does not contain SnapshotJson.");
        }

        var profile = await _profileResolver.ResolveAsync(
            CustomerInsightAiSummaryContract.UseCase,
            CustomerInsightAiSummaryContract.PromptName,
            cancellationToken: cancellationToken);

        var narrativeVersion = CustomerInsightAiSummaryContract.BuildNarrativeVersion(profile.ModelId);
        var current = await _dbContext.CustomerInsightAiSummaries
            .FirstOrDefaultAsync(
                x => x.CustomerInsightSnapshotId == customerInsightSnapshotId
                    && x.Status == CustomerInsightAiSummaryContract.StatusCurrent,
                cancellationToken);

        if (current is not null && current.NarrativeVersion == narrativeVersion)
        {
            return await _summaryReader.GetSummaryAsync(current.Id, cancellationToken)
                ?? throw new InvalidOperationException($"Customer insight AI summary {current.Id} was not found after generation.");
        }

        var inputRefsJson = JsonSerializer.Serialize(new
        {
            SnapshotId = snapshot.Id,
            snapshot.UserId,
            snapshot.AsOfUtc,
            snapshot.WindowStartUtc,
            snapshot.WindowEndUtc,
            snapshot.Version
        }, JsonOptions);

        var aiRunId = await _aiRunWriter.StartRunAsync(
            CustomerInsightAiSummaryContract.UseCase,
            inputRefsJson,
            cancellationToken);

        try
        {
            var snapshotJson = JsonSerializer.Serialize(snapshot.Snapshot, JsonOptions);
            var userPrompt = (profile.UserPromptTemplate ?? "{{SNAPSHOT_JSON}}")
                .Replace("{{SNAPSHOT_JSON}}", snapshotJson);

            var messages = new List<ChatMessage>();
            if (!string.IsNullOrWhiteSpace(profile.SystemPrompt))
            {
                messages.Add(new ChatMessage(ChatRole.System, profile.SystemPrompt));
            }

            messages.Add(new ChatMessage(ChatRole.User, userPrompt));

            var schema = JsonDocument.Parse(CustomerInsightAiSummaryContract.SummaryJsonSchema).RootElement;
            var chatOptions = new ChatOptions
            {
                ModelId = profile.ModelId,
                ResponseFormat = ChatResponseFormat.ForJsonSchema(
                    schema,
                    schemaName: "CustomerInsightAiSummary",
                    schemaDescription: "A structured AI summary of a customer insight snapshot.")
            };
            var response = await _chatClient.GetResponseAsync(messages, chatOptions, cancellationToken);
            var responseText = response.Text ?? string.Empty;

            _logger.LogInformation(
                "AI summary LLM response for snapshot {SnapshotId}: length={Length}, first100={First100}",
                customerInsightSnapshotId,
                responseText.Length,
                responseText.Length > 100 ? responseText[..100] : responseText);

            var generated = GenerateSummary(responseText, narrativeVersion);

            var entity = new CustomerInsightAiSummary
            {
                TenantId = snapshot.Snapshot.TenantId,
                UserId = snapshot.UserId,
                CustomerInsightSnapshotId = snapshot.Id,
                AiRunId = aiRunId,
                Status = CustomerInsightAiSummaryContract.StatusCurrent,
                AsOfUtc = snapshot.AsOfUtc,
                NarrativeVersion = generated.NarrativeVersion,
                SummaryJson = generated.SummaryJson
            };

            _dbContext.CustomerInsightAiSummaries.Add(entity);

            if (current is not null)
            {
                current.Status = CustomerInsightAiSummaryContract.StatusSuperseded;
                current.SupersededById = entity.Id;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await _aiRunWriter.MarkRunCompletedAsync(
                aiRunId,
                $"customer-insight-ai-summary:{entity.Id}",
                cancellationToken);

            return await _summaryReader.GetSummaryAsync(entity.Id, cancellationToken)
                ?? throw new InvalidOperationException($"Customer insight AI summary {entity.Id} was not found after persistence.");
        }
        catch (OperationCanceledException)
        {
            return await PersistFailedSummaryAsync(
                snapshot,
                current,
                aiRunId,
                narrativeVersion,
                "Customer insight AI summary generation timed out or was cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Customer insight AI summary generation failed for snapshot {SnapshotId}.",
                customerInsightSnapshotId);

            return await PersistFailedSummaryAsync(
                snapshot,
                current,
                aiRunId,
                narrativeVersion,
                ex.Message);
        }
    }

    private static GeneratedCustomerInsightAiSummary GenerateSummary(string responseText, string narrativeVersion)
    {
        var cleanedJson = StripJsonFences(responseText);
        var summary = JsonSerializer.Deserialize<CustomerInsightAiSummaryDocument>(cleanedJson, JsonOptions)
            ?? throw new InvalidOperationException("AI summary schema validation failed: response body was empty.");

        Validate(summary);

        var normalized = summary with
        {
            SchemaVersion = CustomerInsightAiSummaryContract.SchemaVersion,
            KeyObservations = summary.KeyObservations.ToList(),
            PositivePatterns = summary.PositivePatterns.ToList(),
            RiskPatterns = summary.RiskPatterns.ToList(),
            RecommendedFocusAreas = summary.RecommendedFocusAreas.ToList(),
            ConversationSuggestions = summary.ConversationSuggestions.ToList(),
            ReferencedMetrics = summary.ReferencedMetrics.ToList(),
            Caveats = summary.Caveats.ToList()
        };

        return new GeneratedCustomerInsightAiSummary(
            narrativeVersion,
            JsonSerializer.Serialize(normalized, JsonOptions),
            normalized);
    }

    private static void Validate(CustomerInsightAiSummaryDocument summary)
    {
        if (string.IsNullOrWhiteSpace(summary.Headline)
            || string.IsNullOrWhiteSpace(summary.Summary)
            || summary.KeyObservations is null
            || summary.PositivePatterns is null
            || summary.RiskPatterns is null
            || summary.RecommendedFocusAreas is null
            || summary.ConversationSuggestions is null
            || summary.ReferencedMetrics is null
            || summary.Caveats is null)
        {
            throw new InvalidOperationException("AI summary schema validation failed: required fields are missing.");
        }
    }

    private async Task<CustomerInsightAiSummaryResponse> PersistFailedSummaryAsync(
        Aonik.Finance.Contracts.Models.PersonalFinance.CustomerInsightSnapshotResponse snapshot,
        CustomerInsightAiSummary? current,
        Guid aiRunId,
        string narrativeVersion,
        string failureReason)
    {
        await TryMarkRunFailedAsync(aiRunId, failureReason);

        var failed = new CustomerInsightAiSummary
        {
            TenantId = snapshot.Snapshot!.TenantId,
            UserId = snapshot.UserId,
            CustomerInsightSnapshotId = snapshot.Id,
            AiRunId = aiRunId,
            Status = CustomerInsightAiSummaryContract.StatusFailed,
            AsOfUtc = _clock.UtcNow,
            NarrativeVersion = narrativeVersion,
            SummaryJson = string.Empty,
            FailureReason = TruncateFailureReason(failureReason)
        };

        if (current is not null)
        {
            _dbContext.Entry(current).State = EntityState.Unchanged;
        }

        _dbContext.CustomerInsightAiSummaries.Add(failed);
        await _dbContext.SaveChangesAsync(CancellationToken.None);

        return await _summaryReader.GetSummaryAsync(failed.Id, CancellationToken.None)
            ?? new CustomerInsightAiSummaryResponse(
                failed.Id,
                failed.UserId,
                failed.CustomerInsightSnapshotId,
                failed.AiRunId,
                failed.Status,
                failed.AsOfUtc,
                failed.NarrativeVersion,
                failed.FailureReason,
                failed.SupersededById,
                failed.CreatedAt,
                failed.UpdatedAt,
                null);
    }

    private async Task TryMarkRunFailedAsync(Guid aiRunId, string failureReason)
    {
        try
        {
            await _aiRunWriter.MarkRunFailedAsync(aiRunId, TruncateFailureReason(failureReason), CancellationToken.None);
        }
        catch
        {
        }
    }

    private static string StripJsonFences(string responseText)
    {
        var trimmed = responseText.Trim();

        // Strip markdown code fences if present.
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

        // If the result already looks like a JSON object, return as-is.
        if (trimmed.StartsWith('{'))
        {
            return trimmed;
        }

        // The LLM may have included prose before/after the JSON object.
        // Extract the outermost { ... } block.
        var firstBrace = trimmed.IndexOf('{');
        var lastBrace = trimmed.LastIndexOf('}');
        if (firstBrace >= 0 && lastBrace > firstBrace)
        {
            return trimmed[firstBrace..(lastBrace + 1)];
        }

        return trimmed;
    }

    private static string TruncateFailureReason(string failureReason)
    {
        if (string.IsNullOrWhiteSpace(failureReason))
        {
            return "Unknown AI summary generation error.";
        }

        var normalized = failureReason.Trim();
        return normalized.Length <= 1000 ? normalized : normalized[..1000];
    }
}
