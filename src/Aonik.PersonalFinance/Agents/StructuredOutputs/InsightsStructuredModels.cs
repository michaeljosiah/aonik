using System.Text.Json;

namespace Aonik.Finance.Agents.StructuredOutputs;

/// <summary>
/// Output contract for the <c>pf-insights</c> sub-agent (Spec 025 §5.1).
/// Collapses today's <c>spending-intelligence.v1</c> + the audit portion of
/// <c>obligation-planning.v1</c> into one unified shape that covers
/// <c>explain</c>, <c>audit</c>, and <c>rank</c> kinds without becoming a
/// kitchen-sink type.
/// </summary>
/// <remarks>
/// Design notes for Open Decision §11.1:
/// <list type="bullet">
///   <item><description>The discriminator is the top-level <c>kind</c> field. Each kind reuses the same outer envelope (summary, confidence, entities, recommendedActions, warnings) but the per-kind numeric payload lives in the free-form <c>metrics</c> bag so we don't bake every metric shape into the schema up-front.</description></item>
///   <item><description><c>entities</c> uses the spec's <c>{ ref, label }</c> shape with a typed reference string like <c>txn:abc</c> or <c>bill:xyz</c> rather than the heavier <c>entityType + entityId</c> pair from the v1 schemas.</description></item>
///   <item><description>Each recommended action carries a <c>simiTool</c> name + optional <c>argsHint</c> so Simi can offer the user a one-click follow-up via <c>display_option_selector</c> after paraphrasing the analysis. <c>argsHint</c> is intentionally schema-less (<see cref="JsonElement"/>) because the args vary by tool.</description></item>
///   <item><description>Output JSON uses camelCase to stay consistent with the rest of Aonik's structured output (despite the spec sketch using snake_case).</description></item>
/// </list>
/// </remarks>
internal static class InsightsStructuredOutputContract
{
    public const string SchemaVersion = "pf_insights.v1";

    public const string JsonSchema = """
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "$id": "aonik.finance.agents.personal-finance.insights.v1",
  "title": "InsightsResult",
  "type": "object",
  "required": [
    "schemaVersion",
    "kind",
    "summary",
    "confidence",
    "reasonCodes",
    "metrics",
    "entities",
    "recommendedActions",
    "warnings"
  ],
  "properties": {
    "schemaVersion": { "type": "string", "const": "pf_insights.v1" },
    "kind": {
      "type": "string",
      "enum": ["explain", "audit", "rank"],
      "description": "Discriminator for the analysis style. explain = why something happened; audit = walk-and-flag (e.g. subscription drift); rank = ordered list by some criterion."
    },
    "summary": {
      "type": "string",
      "description": "1-2 sentence narrative for Simi to paraphrase. Plain text only."
    },
    "confidence": {
      "type": "number",
      "minimum": 0.0,
      "maximum": 1.0,
      "description": "Sub-agent's confidence in the analysis. Below 0.6 means Simi should hedge in the user-facing reply."
    },
    "reasonCodes": {
      "type": "array",
      "items": { "type": "string" },
      "description": "Machine-readable reason codes for observability (e.g. 'category_breakdown_available', 'snapshot_signals_available')."
    },
    "metrics": {
      "type": "object",
      "description": "Free-form supporting numbers whose keys depend on kind. explain typically includes top_driver / delta_pct / period_total / currency; audit typically includes items_reviewed / items_flagged; rank typically includes rank_by / rank_length. Schema-less by design — see Spec 025 §11.1."
    },
    "entities": {
      "type": "array",
      "description": "Entities the analysis touched, in the order Simi should mention them. Each entity is a typed ref string (e.g. 'txn:abc', 'bill:xyz', 'category:dining') plus a human label.",
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
    "recommendedActions": {
      "type": "array",
      "description": "0-6 follow-up actions Simi can offer. Each carries the user-facing label, the Simi-side tool that would carry it out, and an optional args hint for pre-filling. Schema for argsHint is intentionally free-form because it varies by tool.",
      "items": {
        "type": "object",
        "required": ["label"],
        "properties": {
          "label": { "type": "string" },
          "simiTool": { "type": ["string", "null"] },
          "argsHint": { "type": ["object", "null"] }
        },
        "additionalProperties": false
      }
    },
    "warnings": {
      "type": "array",
      "items": { "type": "string" },
      "description": "Human-readable issues that limited the analysis (e.g. 'no snapshot available for previous period')."
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
/// Input shape Simi serialises when invoking <c>pf-insights</c> via her
/// <c>pf_run_insights</c> tool (wired in Spec 025 Phase 5). <c>Kind</c> is
/// optional — when null, the sub-agent picks an appropriate analysis style
/// based on <c>UserQuestion</c>.
/// </summary>
internal sealed record InsightsRequest(
    string UserQuestion,
    string? Kind,
    DateTime? PeriodStart,
    DateTime? PeriodEnd,
    Guid? PersonalAccountId);

/// <summary>
/// Top-level structured output from <c>pf-insights</c>. Conforms to
/// <see cref="InsightsStructuredOutputContract.JsonSchema"/>.
/// </summary>
internal sealed record InsightsResult(
    string SchemaVersion,
    string Kind,
    string Summary,
    decimal Confidence,
    IReadOnlyList<string> ReasonCodes,
    JsonElement Metrics,
    IReadOnlyList<InsightsEntity> Entities,
    IReadOnlyList<InsightsRecommendedAction> RecommendedActions,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Typed entity reference. <c>Ref</c> uses the format <c>type:id</c> —
/// supported types include <c>txn</c>, <c>bill</c>, <c>commitment</c>,
/// <c>category</c>, <c>merchant</c>, <c>account</c>, <c>snapshot</c>.
/// </summary>
internal sealed record InsightsEntity(
    string Ref,
    string Label);

/// <summary>
/// One Simi-actionable follow-up. <c>SimiTool</c> names a tool from Simi's
/// catalogue (e.g. <c>pf_archive_bill</c>, <c>pf_update_bill</c>); <c>ArgsHint</c>
/// pre-fills tool arguments where the sub-agent already knows the IDs. Both are
/// optional — informational-only recommendations omit them.
/// </summary>
internal sealed record InsightsRecommendedAction(
    string Label,
    string? SimiTool,
    JsonElement? ArgsHint);

/// <summary>
/// Wrapper Simi receives back from the <c>pf_run_insights</c> tool —
/// strongly-typed result plus raw JSON for observability + audit trails.
/// </summary>
internal sealed record InsightsAgentToolResponse(
    InsightsResult Analysis,
    string AnalysisJson);
