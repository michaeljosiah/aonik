using System.Text.Json;

namespace Aonik.Finance.Agents.StructuredOutputs;

internal static class ObligationPlanningStructuredOutputContract
{
    public const string SchemaVersion = "pf_obligation_planning.v1";

    public const string JsonSchema = """
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "$id": "aonik.finance.agents.personal-finance.obligation-planning.v1",
  "title": "ObligationPlanningResult",
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
        "lookaheadDays",
        "upcomingObligations",
        "obligationTotals",
        "coverageSignals",
        "snapshotSignals",
        "householdContext"
      ],
      "properties": {
        "lookaheadDays": { "type": "integer" },
        "upcomingObligations": {
          "type": "array",
          "items": {
            "type": "object",
            "required": ["itemType", "sourceId", "displayName", "currency", "dueDate", "daysUntilDue", "status"],
            "properties": {
              "itemType": { "type": "string" },
              "sourceId": { "type": "string" },
              "displayName": { "type": "string" },
              "amount": { "type": ["number", "null"] },
              "currency": { "type": "string" },
              "dueDate": { "type": "string", "format": "date-time" },
              "daysUntilDue": { "type": "integer" },
              "status": { "type": "string" }
            },
            "additionalProperties": false
          }
        },
        "obligationTotals": {
          "type": "array",
          "items": {
            "type": "object",
            "required": ["currency", "totalAmount", "itemCount"],
            "properties": {
              "currency": { "type": "string" },
              "totalAmount": { "type": "number" },
              "itemCount": { "type": "integer" }
            },
            "additionalProperties": false
          }
        },
        "coverageSignals": {
          "type": "array",
          "items": {
            "type": "object",
            "required": ["currency", "availableBalance", "upcomingObligations"],
            "properties": {
              "currency": { "type": "string" },
              "availableBalance": { "type": "number" },
              "upcomingObligations": { "type": "number" },
              "ratio": { "type": ["number", "null"] }
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
        },
        "householdContext": {
          "type": ["object", "null"],
          "properties": {
            "hasHousehold": { "type": "boolean" },
            "householdId": { "type": ["string", "null"] },
            "memberCount": { "type": "integer" }
          },
          "additionalProperties": false
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

internal sealed record ObligationPlanningRequest(
    string UserQuestion,
    int WithinDays,
    bool IncludeSnapshotSignals,
    bool IncludeHouseholdContext);

internal sealed record ObligationPlanningResult(
    string SchemaVersion,
    string ResultType,
    string Summary,
    decimal Confidence,
    IReadOnlyList<string> ReasonCodes,
    IReadOnlyList<ObligationPlanningEntityReference> EntityRefs,
    IReadOnlyList<ObligationPlanningRecommendedAction> RecommendedActions,
    IReadOnlyList<string> Warnings,
    ObligationPlanningPayload Payload);

internal sealed record ObligationPlanningEntityReference(
    string EntityType,
    string EntityId,
    string? Label);

internal sealed record ObligationPlanningRecommendedAction(
    string ActionType,
    string Title,
    string Description,
    string Priority,
    string? RelatedEntityId);

internal sealed record ObligationPlanningPayload(
    int LookaheadDays,
    IReadOnlyList<ObligationPlanningObligation> UpcomingObligations,
    IReadOnlyList<ObligationPlanningCurrencyTotal> ObligationTotals,
    IReadOnlyList<ObligationPlanningCoverageSignal> CoverageSignals,
    IReadOnlyList<ObligationPlanningSnapshotSignal> SnapshotSignals,
    ObligationPlanningHouseholdContext? HouseholdContext);

internal sealed record ObligationPlanningObligation(
    string ItemType,
    Guid SourceId,
    string DisplayName,
    decimal? Amount,
    string Currency,
    DateTime DueDate,
    int DaysUntilDue,
    string Status);

internal sealed record ObligationPlanningCurrencyTotal(
    string Currency,
    decimal TotalAmount,
    int ItemCount);

internal sealed record ObligationPlanningCoverageSignal(
    string Currency,
    decimal AvailableBalance,
    decimal UpcomingObligations,
    decimal? Ratio);

internal sealed record ObligationPlanningSnapshotSignal(
    string SignalKey,
    string Category,
    string Title,
    string Description,
    string Severity,
    string Confidence);

internal sealed record ObligationPlanningHouseholdContext(
    bool HasHousehold,
    Guid? HouseholdId,
    int MemberCount);

internal sealed record ObligationPlanningAgentToolResponse(
    ObligationPlanningResult Analysis,
    string AnalysisJson);
