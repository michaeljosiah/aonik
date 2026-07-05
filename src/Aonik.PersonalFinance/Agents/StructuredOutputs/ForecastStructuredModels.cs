using System.Text.Json;

namespace Aonik.PersonalFinance.Agents.StructuredOutputs;

/// <summary>
/// Output contract for the <c>pf-forecast</c> sub-agent (Spec 025 §5.2).
/// Models forward projections and parametric scenarios — "will I be okay
/// for rent", savings ETAs, and what-if simulations.
/// </summary>
/// <remarks>
/// Design notes for Open Decision §11.2:
/// <list type="bullet">
///   <item><description>The shape is more strongly-typed than <c>insights.v1</c> because forecast outputs are arithmetic — every number has a fixed role (projected income, committed bills, run-rate spend, etc.). The schema enforces that structure.</description></item>
///   <item><description><c>result.verdict</c> is a 3-way enum (<c>short</c>, <c>covered</c>, <c>tight</c>) rather than a free string, so Simi can pick the right visual treatment in her reply (e.g. red badge for short).</description></item>
///   <item><description><c>options[]</c> drives <c>display_option_selector</c> in Simi's reply. Each option carries a numeric <c>delta</c> (positive = improves the verdict, negative = worsens) plus an optional <c>simiTool</c> the user can authorise via <c>confirmAction</c> if they pick it.</description></item>
///   <item><description><c>assumptions[]</c> is plain text so Simi can quote one or two when appropriate ("assuming income on 25th matches last three months").</description></item>
/// </list>
/// </remarks>
internal static class ForecastStructuredOutputContract
{
    public const string SchemaVersion = "pf_forecast.v1";

    public const string JsonSchema = """
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "$id": "aonik.finance.agents.personal-finance.forecast.v1",
  "title": "ForecastResult",
  "type": "object",
  "required": [
    "schemaVersion",
    "scenario",
    "result",
    "assumptions",
    "breakdown",
    "options",
    "confidence",
    "reasonCodes",
    "warnings"
  ],
  "properties": {
    "schemaVersion": { "type": "string", "const": "pf_forecast.v1" },
    "scenario": {
      "type": "string",
      "description": "Short human label for the modelled scenario (e.g. 'Rent coverage on 30 April', 'Emergency fund target ETA')."
    },
    "result": {
      "type": "object",
      "required": ["verdict", "amount", "currency"],
      "description": "Headline answer. amount is signed: negative when short, positive when covered with buffer, ~0 when tight.",
      "properties": {
        "verdict": {
          "type": "string",
          "enum": ["short", "covered", "tight"]
        },
        "amount": { "type": "number" },
        "currency": { "type": "string", "description": "ISO 4217 code." }
      },
      "additionalProperties": false
    },
    "assumptions": {
      "type": "array",
      "items": { "type": "string" },
      "description": "Plain-text assumptions baked into the projection. Simi may quote 1-2 in her reply if the user pushes back."
    },
    "breakdown": {
      "type": "array",
      "description": "Ordered line items composing the result amount. Signs are consistent: inflows positive, outflows negative.",
      "items": {
        "type": "object",
        "required": ["label", "amount"],
        "properties": {
          "label": { "type": "string" },
          "amount": { "type": "number" }
        },
        "additionalProperties": false
      }
    },
    "options": {
      "type": "array",
      "description": "0-6 user-actionable moves that would change the verdict. Drives display_option_selector in Simi's reply.",
      "items": {
        "type": "object",
        "required": ["label", "delta"],
        "properties": {
          "label": { "type": "string" },
          "delta": {
            "type": "number",
            "description": "Signed change to result.amount if the user picks this option. Positive = improves, negative = worsens."
          },
          "simiTool": { "type": ["string", "null"] },
          "argsHint": { "type": ["object", "null"] }
        },
        "additionalProperties": false
      }
    },
    "confidence": {
      "type": "number",
      "minimum": 0.0,
      "maximum": 1.0,
      "description": "Sub-agent's confidence in the projection. Below 0.6 means Simi should hedge ('roughly', 'on current trends')."
    },
    "reasonCodes": {
      "type": "array",
      "items": { "type": "string" }
    },
    "warnings": {
      "type": "array",
      "items": { "type": "string" }
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
/// Input shape Simi serialises when invoking <c>pf-forecast</c> via her
/// <c>pf_run_forecast</c> tool (wired in Spec 025 Phase 5).
/// </summary>
internal sealed record ForecastRequest(
    string UserQuestion,
    DateTime? AsOfDate,
    int? HorizonDays);

/// <summary>
/// Top-level structured output from <c>pf-forecast</c>. Conforms to
/// <see cref="ForecastStructuredOutputContract.JsonSchema"/>.
/// </summary>
internal sealed record ForecastResult(
    string SchemaVersion,
    string Scenario,
    ForecastVerdict Result,
    IReadOnlyList<string> Assumptions,
    IReadOnlyList<ForecastBreakdownLine> Breakdown,
    IReadOnlyList<ForecastOption> Options,
    decimal Confidence,
    IReadOnlyList<string> ReasonCodes,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Headline answer. <c>Verdict</c> is one of <c>short</c> / <c>covered</c> /
/// <c>tight</c>; <c>Amount</c> is signed (negative when short of obligation,
/// positive with buffer, ~0 when tight).
/// </summary>
internal sealed record ForecastVerdict(
    string Verdict,
    decimal Amount,
    string Currency);

/// <summary>
/// One line in the breakdown that composes <see cref="ForecastVerdict.Amount"/>.
/// Inflows positive, outflows negative.
/// </summary>
internal sealed record ForecastBreakdownLine(
    string Label,
    decimal Amount);

/// <summary>
/// One user-actionable move that would change the verdict. <c>Delta</c> is
/// signed (positive = improves the headline number, negative = worsens).
/// <c>SimiTool</c> + <c>ArgsHint</c> let Simi offer a one-click follow-up
/// gated by <c>confirmAction</c>.
/// </summary>
internal sealed record ForecastOption(
    string Label,
    decimal Delta,
    string? SimiTool,
    JsonElement? ArgsHint);

/// <summary>
/// Wrapper Simi receives back from the <c>pf_run_forecast</c> tool —
/// strongly-typed result plus raw JSON for observability + audit trails.
/// </summary>
internal sealed record ForecastAgentToolResponse(
    ForecastResult Analysis,
    string AnalysisJson);
