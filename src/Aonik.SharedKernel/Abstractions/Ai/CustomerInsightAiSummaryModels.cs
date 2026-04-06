namespace Aonik.SharedKernel.Abstractions.Ai;

public static class CustomerInsightAiSummaryContract
{
    public const string SchemaVersion = "customer_insight_ai_summary.v1";
    public const string PromptName = "customer_insight_summary";
    public const string PromptVersion = "v2";
    public const string UseCase = "personal_finance_customer_insight_summary";

    public const string StatusCurrent = "Current";
    public const string StatusSuperseded = "Superseded";
    public const string StatusFailed = "Failed";

    public const string SummaryJsonSchema = """
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "$id": "aonik.ai.customer-insight-ai-summary.v1",
  "title": "CustomerInsightAiSummary",
  "type": "object",
  "required": [
    "schemaVersion",
    "headline",
    "summary",
    "keyObservations",
    "positivePatterns",
    "riskPatterns",
    "recommendedFocusAreas",
    "conversationSuggestions",
    "referencedMetrics",
    "caveats"
  ],
  "properties": {
    "schemaVersion": { "type": "string" },
    "headline": { "type": "string" },
    "summary": { "type": "string" },
    "keyObservations": { "type": "array", "items": { "type": "string" } },
    "positivePatterns": { "type": "array", "items": { "type": "string" } },
    "riskPatterns": { "type": "array", "items": { "type": "string" } },
    "recommendedFocusAreas": { "type": "array", "items": { "type": "string" } },
    "conversationSuggestions": { "type": "array", "items": { "type": "string" } },
    "referencedMetrics": { "type": "array", "items": { "type": "string" } },
    "caveats": { "type": "array", "items": { "type": "string" } }
  }
}
""";

    public static string BuildNarrativeVersion(string? modelId)
    {
        var resolvedModelId = string.IsNullOrWhiteSpace(modelId) ? "default" : modelId.Trim();
        return $"{SchemaVersion}|prompt:{PromptName}:{PromptVersion}|model:{resolvedModelId}";
    }
}

public record CustomerInsightAiSummaryResponse(
    Guid Id,
    Guid UserId,
    Guid CustomerInsightSnapshotId,
    Guid AiRunId,
    string Status,
    DateTime AsOfUtc,
    string NarrativeVersion,
    string? FailureReason,
    Guid? SupersededById,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    CustomerInsightAiSummaryDocument? Summary);

public record GeneratedCustomerInsightAiSummary(
    string NarrativeVersion,
    string SummaryJson,
    CustomerInsightAiSummaryDocument Summary);

public record CustomerInsightAiSummaryDocument(
    string SchemaVersion,
    string Headline,
    string Summary,
    IReadOnlyList<string> KeyObservations,
    IReadOnlyList<string> PositivePatterns,
    IReadOnlyList<string> RiskPatterns,
    IReadOnlyList<string> RecommendedFocusAreas,
    IReadOnlyList<string> ConversationSuggestions,
    IReadOnlyList<string> ReferencedMetrics,
    IReadOnlyList<string> Caveats);
