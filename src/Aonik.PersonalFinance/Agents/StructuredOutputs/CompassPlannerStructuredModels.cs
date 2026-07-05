using System.Text.Json;

namespace Aonik.PersonalFinance.Agents.StructuredOutputs;

/// <summary>
/// Output contract for the <c>pf-compass-planner</c> sub-agent (Spec 021 §5).
/// The planner turns a goal plus the user's grounded financial context into a
/// structured, reviewable roadmap that populates <c>CompassPlan.PlanJson</c>.
/// Schema-bound like the other PF specialists so the parsed result and its raw
/// JSON travel together for audit + persistence.
/// </summary>
internal static class CompassPlannerStructuredOutputContract
{
    public const string SchemaVersion = "pf_compass_plan.v1";

    public const string JsonSchema = """
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "$id": "aonik.finance.agents.personal-finance.compass-plan.v1",
  "title": "CompassPlanResult",
  "type": "object",
  "required": [
    "schemaVersion",
    "summary",
    "steps",
    "confidence",
    "reasonCodes",
    "entities",
    "warnings"
  ],
  "properties": {
    "schemaVersion": { "type": "string", "const": "pf_compass_plan.v1" },
    "summary": {
      "type": "string",
      "description": "Narrative summary of the plan in plain language Simi can read out."
    },
    "steps": {
      "type": "array",
      "description": "Ordered, recommended steps toward the goal. 1-8 steps.",
      "items": {
        "type": "object",
        "required": ["title", "rationale"],
        "properties": {
          "title": { "type": "string" },
          "rationale": { "type": "string" },
          "suggestedAmount": { "type": ["number", "null"], "description": "Suggested amount for this step, in the goal currency." },
          "currency": { "type": ["string", "null"], "description": "ISO 4217 code for suggestedAmount." },
          "targetDate": { "type": ["string", "null"], "description": "ISO-8601 UTC date this step should complete by." }
        },
        "additionalProperties": false
      }
    },
    "confidence": {
      "type": "number",
      "minimum": 0.0,
      "maximum": 1.0,
      "description": "Planner's confidence in the plan given the data quality. Below 0.6 means Simi hedges."
    },
    "reasonCodes": {
      "type": "array",
      "items": { "type": "string" }
    },
    "entities": {
      "type": "array",
      "description": "Entity references the plan touches (goal, account, commitment ids).",
      "items": {
        "type": "object",
        "required": ["ref", "label"],
        "properties": {
          "ref": { "type": "string" },
          "label": { "type": "string" }
        },
        "additionalProperties": false
      }
    },
    "warnings": {
      "type": "array",
      "items": { "type": "string" },
      "description": "Plain-English notes about missing data or assumptions the planner flagged."
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

/// <summary>
/// Input the plan service serialises when invoking <c>pf-compass-planner</c>.
/// Carries the goal plus the grounded financial context (snapshot summary,
/// obligations, budgets) as a single JSON message — the planner reasons over
/// it directly rather than calling tools.
/// </summary>
internal sealed record CompassPlannerRequest(
    Guid GoalId,
    string GoalName,
    string? GoalType,
    decimal TargetAmount,
    decimal ProgressAmount,
    string Currency,
    DateTime? TargetDate,
    string? RiskAppetite,
    string? Strategy,
    DateTime HorizonStartUtc,
    DateTime HorizonEndUtc,
    CompassPlannerContext Context);

/// <summary>
/// Grounding context handed to the planner — a deterministic projection of the
/// user's snapshot + obligations so the LLM never has to (and cannot) compute
/// the safe-to-spend number itself.
/// </summary>
internal sealed record CompassPlannerContext(
    decimal SafeToSpend,
    decimal LiquidAssets,
    decimal ProtectedObligations,
    string OperatingCurrency,
    bool GuidanceIsPartial,
    IReadOnlyList<string> ObligationLabels,
    IReadOnlyList<string> Warnings);

/// <summary>Top-level structured output from <c>pf-compass-planner</c>.</summary>
internal sealed record CompassPlanResult(
    string SchemaVersion,
    string Summary,
    IReadOnlyList<CompassPlanStep> Steps,
    decimal Confidence,
    IReadOnlyList<string> ReasonCodes,
    IReadOnlyList<CompassPlanEntity> Entities,
    IReadOnlyList<string> Warnings);

internal sealed record CompassPlanStep(
    string Title,
    string Rationale,
    decimal? SuggestedAmount,
    string? Currency,
    DateTime? TargetDate);

internal sealed record CompassPlanEntity(
    string Ref,
    string Label);

/// <summary>
/// Wrapper the plan service / Simi receives from the planner tool — strongly
/// typed result plus raw JSON for observability + persistence into
/// <c>CompassPlan.PlanJson</c>.
/// </summary>
internal sealed record CompassPlannerAgentToolResponse(
    CompassPlanResult Plan,
    string PlanJson);
