using System.Text.Json;

namespace Aonik.Finance.Agents.StructuredOutputs;

/// <summary>
/// Output contract for the <c>pf-classify</c> sub-agent (Spec 025 §5.3).
/// Walks the classification review queue, scores candidate categories per
/// item, and where the pattern is strong proposes a categorisation rule.
/// </summary>
/// <remarks>
/// Design notes:
/// <list type="bullet">
///   <item><description>The sub-agent only proposes — it never applies a category override or creates a rule itself. Simi takes the proposals and runs each per-item correction through her existing <c>confirmAction</c> approval flow.</description></item>
///   <item><description>Each correction surfaces 1-3 ranked <c>suggestions</c> so Simi can offer them via <c>display_option_selector</c> when the top suggestion's confidence isn't decisive.</description></item>
///   <item><description><c>ruleRecommended</c> is optional — only present when the merchant/pattern is strong enough that auto-classifying future transactions is worth offering.</description></item>
///   <item><description><c>match</c> uses a simple expression DSL today (e.g. <c>merchant_name == 'Honest Burgers'</c>) — Simi translates it to a <c>pf_create_categorisation_rule</c> tool call when the user accepts.</description></item>
/// </list>
/// </remarks>
internal static class ClassifyStructuredOutputContract
{
    public const string SchemaVersion = "pf_classify.v1";

    public const string JsonSchema = """
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "$id": "aonik.finance.agents.personal-finance.classify.v1",
  "title": "ClassifyResult",
  "type": "object",
  "required": [
    "schemaVersion",
    "summary",
    "proposedCorrections",
    "confidence",
    "reasonCodes",
    "warnings"
  ],
  "properties": {
    "schemaVersion": { "type": "string", "const": "pf_classify.v1" },
    "summary": {
      "type": "string",
      "description": "Single-line headline for Simi to paraphrase (e.g. '12 items reviewed: 9 confident reclassifications, 3 need your input')."
    },
    "proposedCorrections": {
      "type": "array",
      "description": "0..N per-item proposals. Order is the order Simi should offer them — typically confident corrections first, then ambiguous items needing user input.",
      "items": {
        "type": "object",
        "required": ["txnRef", "label", "suggestions"],
        "properties": {
          "txnRef": {
            "type": "string",
            "description": "Typed ref string for the transaction, format 'txn:{id}'."
          },
          "label": {
            "type": "string",
            "description": "Human label (e.g. 'Honest Burgers · £14.50 · 12 Apr'). Used directly in Simi's UI."
          },
          "currentCategory": { "type": ["string", "null"] },
          "suggestions": {
            "type": "array",
            "minItems": 1,
            "maxItems": 3,
            "items": {
              "type": "object",
              "required": ["category", "confidence"],
              "properties": {
                "category": { "type": "string" },
                "confidence": { "type": "number", "minimum": 0.0, "maximum": 1.0 }
              },
              "additionalProperties": false
            }
          },
          "ruleRecommended": {
            "type": ["object", "null"],
            "required": ["match", "category"],
            "properties": {
              "match": {
                "type": "string",
                "description": "Simple expression describing the match condition (e.g. \"merchant_name == 'Honest Burgers'\")."
              },
              "category": { "type": "string" }
            },
            "additionalProperties": false
          }
        },
        "additionalProperties": false
      }
    },
    "confidence": {
      "type": "number",
      "minimum": 0.0,
      "maximum": 1.0,
      "description": "Aggregate confidence in the proposed corrections set as a whole."
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
/// Input shape Simi serialises when invoking <c>pf-classify</c> via her
/// <c>pf_run_classify_review</c> tool (wired in Spec 025 Phase 5).
/// </summary>
internal sealed record ClassifyRequest(
    string UserQuestion,
    int? MaxItems,
    Guid? PersonalAccountId);

/// <summary>
/// Top-level structured output from <c>pf-classify</c>. Conforms to
/// <see cref="ClassifyStructuredOutputContract.JsonSchema"/>.
/// </summary>
internal sealed record ClassifyResult(
    string SchemaVersion,
    string Summary,
    IReadOnlyList<ClassifyProposedCorrection> ProposedCorrections,
    decimal Confidence,
    IReadOnlyList<string> ReasonCodes,
    IReadOnlyList<string> Warnings);

/// <summary>
/// One per-item proposal. <c>TxnRef</c> uses the typed ref format
/// <c>txn:{id}</c>; <c>Label</c> is the human-facing identifier Simi shows
/// in the UI; <c>Suggestions</c> is 1-3 ranked candidate categories;
/// <c>RuleRecommended</c> is optional and only present when the merchant/pattern
/// is strong enough to justify creating a categorisation rule alongside the
/// per-transaction override.
/// </summary>
internal sealed record ClassifyProposedCorrection(
    string TxnRef,
    string Label,
    string? CurrentCategory,
    IReadOnlyList<ClassifySuggestion> Suggestions,
    ClassifyRuleRecommendation? RuleRecommended);

/// <summary>
/// One candidate category with a confidence score. Highest-confidence
/// suggestion appears first in the list.
/// </summary>
internal sealed record ClassifySuggestion(
    string Category,
    decimal Confidence);

/// <summary>
/// Optional rule recommendation accompanying a proposed correction.
/// <c>Match</c> is a simple expression DSL (e.g. <c>merchant_name == 'Honest Burgers'</c>);
/// Simi translates it to the appropriate <c>pf_create_categorisation_rule</c>
/// arguments if the user accepts.
/// </summary>
internal sealed record ClassifyRuleRecommendation(
    string Match,
    string Category);

/// <summary>
/// Wrapper Simi receives back from the <c>pf_run_classify_review</c> tool —
/// strongly-typed result plus raw JSON for observability + audit trails.
/// </summary>
internal sealed record ClassifyAgentToolResponse(
    ClassifyResult Analysis,
    string AnalysisJson);
