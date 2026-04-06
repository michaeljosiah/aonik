using System.Text.Json;

namespace Aonik.Finance.Agents.StructuredOutputs;

internal static class SpendingIntelligenceStructuredOutputContract
{
    public const string SchemaVersion = "pf_spending_intelligence.v1";

    public const string JsonSchema = """
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "$id": "aonik.finance.agents.personal-finance.spending-intelligence.v1",
  "title": "SpendingIntelligenceResult",
  "type": "object",
  "required": [
    "schemaVersion",
    "resultType",
    "summary",
    "confidence",
    "reasonCodes",
    "entityRefs",
    "recommendedActions",
    "warnings",
    "payload"
  ],
  "properties": {
    "schemaVersion": { "type": "string" },
    "resultType": { "type": "string" },
    "summary": { "type": "string" },
    "confidence": { "type": "number" },
    "reasonCodes": { "type": "array", "items": { "type": "string" } },
    "entityRefs": {
      "type": "array",
      "items": {
        "type": "object",
        "required": ["entityType", "entityId"],
        "properties": {
          "entityType": { "type": "string" },
          "entityId": { "type": "string" },
          "label": { "type": ["string", "null"] }
        },
        "additionalProperties": false
      }
    },
    "recommendedActions": {
      "type": "array",
      "items": {
        "type": "object",
        "required": ["actionType", "title", "description", "priority"],
        "properties": {
          "actionType": { "type": "string" },
          "title": { "type": "string" },
          "description": { "type": "string" },
          "priority": { "type": "string" },
          "relatedEntityId": { "type": ["string", "null"] }
        },
        "additionalProperties": false
      }
    },
    "warnings": { "type": "array", "items": { "type": "string" } },
    "payload": {
      "type": "object",
      "required": [
        "analysisWindow",
        "narrative",
        "spendingSummary",
        "topCategories",
        "topMerchants",
        "budgetSignals",
        "snapshotSignals"
      ],
      "properties": {
        "analysisWindow": {
          "type": "object",
          "required": ["periodStart", "periodEnd"],
          "properties": {
            "periodStart": { "type": "string", "format": "date-time" },
            "periodEnd": { "type": "string", "format": "date-time" },
            "personalAccountId": { "type": ["string", "null"] }
          },
          "additionalProperties": false
        },
        "narrative": {
          "type": ["object", "null"],
          "properties": {
            "insightId": { "type": "string" },
            "aiRunId": { "type": "string" },
            "title": { "type": "string" },
            "summary": { "type": "string" },
            "createdUtc": { "type": "string", "format": "date-time" }
          },
          "additionalProperties": false
        },
        "spendingSummary": {
          "type": "object",
          "required": [
            "currency",
            "totalIncome",
            "totalExpense",
            "netAmount",
            "transactionCount"
          ],
          "properties": {
            "currency": { "type": "string" },
            "totalIncome": { "type": "number" },
            "totalExpense": { "type": "number" },
            "netAmount": { "type": "number" },
            "transactionCount": { "type": "integer" }
          },
          "additionalProperties": false
        },
        "topCategories": {
          "type": "array",
          "items": {
            "type": "object",
            "required": ["category", "totalAmount", "percentage", "transactionCount"],
            "properties": {
              "category": { "type": "string" },
              "totalAmount": { "type": "number" },
              "percentage": { "type": "number" },
              "transactionCount": { "type": "integer" }
            },
            "additionalProperties": false
          }
        },
        "topMerchants": {
          "type": "array",
          "items": {
            "type": "object",
            "required": ["merchant", "totalAmount", "transactionCount"],
            "properties": {
              "merchant": { "type": "string" },
              "totalAmount": { "type": "number" },
              "transactionCount": { "type": "integer" }
            },
            "additionalProperties": false
          }
        },
        "budgetSignals": {
          "type": "array",
          "items": {
            "type": "object",
            "required": ["category", "limitAmount", "spentAmount", "percentUsed", "isProjectedToOverspend"],
            "properties": {
              "category": { "type": "string" },
              "limitAmount": { "type": "number" },
              "spentAmount": { "type": "number" },
              "percentUsed": { "type": "number" },
              "isProjectedToOverspend": { "type": "boolean" }
            },
            "additionalProperties": false
          }
        },
        "snapshotSignals": {
          "type": "array",
          "items": {
            "type": "object",
            "required": ["signalKey", "category", "title", "description", "severity", "confidence"],
            "properties": {
              "signalKey": { "type": "string" },
              "category": { "type": "string" },
              "title": { "type": "string" },
              "description": { "type": "string" },
              "severity": { "type": "string" },
              "confidence": { "type": "string" }
            },
            "additionalProperties": false
          }
        }
      },
      "additionalProperties": false
    }
  },
  "additionalProperties": false
}
""";

    public static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
    };
}

internal sealed record SpendingIntelligenceRequest(
    string UserQuestion,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    Guid? PersonalAccountId,
    bool IncludeNarrative,
    bool IncludeSnapshotSignals,
    bool IncludeBudgetSignals);

internal sealed record SpendingIntelligenceResult(
    string SchemaVersion,
    string ResultType,
    string Summary,
    decimal Confidence,
    IReadOnlyList<string> ReasonCodes,
    IReadOnlyList<SpendingIntelligenceEntityReference> EntityRefs,
    IReadOnlyList<SpendingIntelligenceRecommendedAction> RecommendedActions,
    IReadOnlyList<string> Warnings,
    SpendingIntelligencePayload Payload);

internal sealed record SpendingIntelligenceEntityReference(
    string EntityType,
    string EntityId,
    string? Label);

internal sealed record SpendingIntelligenceRecommendedAction(
    string ActionType,
    string Title,
    string Description,
    string Priority,
    string? RelatedEntityId);

internal sealed record SpendingIntelligencePayload(
    SpendingIntelligenceAnalysisWindow AnalysisWindow,
    SpendingIntelligenceNarrative? Narrative,
    SpendingIntelligenceSummary SpendingSummary,
    IReadOnlyList<SpendingIntelligenceCategory> TopCategories,
    IReadOnlyList<SpendingIntelligenceMerchant> TopMerchants,
    IReadOnlyList<SpendingIntelligenceBudgetSignal> BudgetSignals,
    IReadOnlyList<SpendingIntelligenceSnapshotSignal> SnapshotSignals);

internal sealed record SpendingIntelligenceAnalysisWindow(
    DateTime PeriodStart,
    DateTime PeriodEnd,
    Guid? PersonalAccountId);

internal sealed record SpendingIntelligenceNarrative(
    Guid InsightId,
    Guid AiRunId,
    string Title,
    string Summary,
    DateTime CreatedUtc);

internal sealed record SpendingIntelligenceSummary(
    string Currency,
    decimal TotalIncome,
    decimal TotalExpense,
    decimal NetAmount,
    int TransactionCount);

internal sealed record SpendingIntelligenceCategory(
    string Category,
    decimal TotalAmount,
    decimal Percentage,
    int TransactionCount);

internal sealed record SpendingIntelligenceMerchant(
    string Merchant,
    decimal TotalAmount,
    int TransactionCount);

internal sealed record SpendingIntelligenceBudgetSignal(
    string Category,
    decimal LimitAmount,
    decimal SpentAmount,
    decimal PercentUsed,
    bool IsProjectedToOverspend);

internal sealed record SpendingIntelligenceSnapshotSignal(
    string SignalKey,
    string Category,
    string Title,
    string Description,
    string Severity,
    string Confidence);

internal sealed record SpendingIntelligenceAgentToolResponse(
    SpendingIntelligenceResult Analysis,
    string AnalysisJson);
