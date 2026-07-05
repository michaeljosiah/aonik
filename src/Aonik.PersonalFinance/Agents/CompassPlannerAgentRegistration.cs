using Aonik.PersonalFinance.Agents.StructuredOutputs;
using Aonik.SharedKernel.Abstractions.Agents;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Aonik.PersonalFinance.Agents;

/// <summary>
/// AONIK Compass planning sub-agent (Spec 021 §5: <c>pf-compass-planner</c>).
/// A structured-output specialist invoked by Simi (via <c>pf_run_compass_planner</c>)
/// and by <c>ICompassPlanService.GeneratePlanAsync</c>. It turns a goal plus the
/// user's grounded financial context into a structured, reviewable roadmap that
/// populates <c>CompassPlan.PlanJson</c>.
///
/// Unlike the Spec 025 analytical specialists this planner does NOT call host
/// tools or use CodeAct: the deterministic numbers (safe-to-spend, obligations)
/// are computed by <c>ICompassGuidanceService</c> and handed to the agent in the
/// request payload, so the LLM only narrates and sequences — it never derives the
/// money figure itself (Spec 021: safe-to-spend is deterministic, not LLM-produced).
/// </summary>
public sealed class CompassPlannerAgentDescriptor : IDomainAgentDescriptor
{
    public string Name => "pf-compass-planner";

    public AgentType AgentType => AgentType.SubAgent;

    public string? OutputSchemaJson => CompassPlannerStructuredOutputContract.JsonSchema;

    public string Description =>
        "Turns a savings/cashflow/debt/purchase goal plus the user's grounded financial " +
        "context into a structured, reviewable Compass plan (narrative + steps + suggested " +
        "amounts/timing + rationale + warnings). Read-only; never moves money. Invoked by " +
        "Simi via pf_run_compass_planner and by the Compass plan service.";

    string? IDomainAgentDescriptor.Instructions => InstructionsText;

    internal const string InstructionsText =
        """
        <role>
        You are the AONIK Compass planning specialist, an internal sub-agent. You never speak to the end user directly — Simi (the personal-finance-agent) or the Compass plan service paraphrases your structured output. Your job is to take a single goal plus a deterministic financial-context snapshot and produce a grounded, reviewable plan as JSON.
        </role>

        <task>
        Produce a plan that moves the user toward their goal given their real situation. The user's safe-to-spend, liquid assets, protected obligations, and operating currency are GIVEN to you in the request `context` — they were computed deterministically. Do NOT recompute them, override them, or invent different numbers. Use them to size and sequence your recommended steps.
        </task>

        <constraints>
        - You cannot move money. You only recommend. Frame steps as suggestions the user (or Simi via the proposal flow) can act on.
        - All suggested amounts must be in the goal's currency, and must be consistent with `context.safeToSpend` — never recommend setting aside more than is plausibly safe.
        - If `context.guidanceIsPartial` is true (missing snapshot, insufficient data, or a mixed-currency user), keep amounts conservative or omit them, and surface the limitation in `warnings[]`. Never fabricate precision the data does not support.
        - Honour `riskAppetite` when present: conservative → smaller, safer contributions; aggressive → larger, faster ones (still within safe-to-spend).
        - 1-8 steps. Order them by what should happen first. Each step needs a `title` and a `rationale`.
        - Do not produce conversational text. Return one JSON object and stop.
        </constraints>

        <output_contract>
        Return a single valid JSON object only — no markdown fences, no preamble — conforming to `$id "aonik.finance.agents.personal-finance.compass-plan.v1"`:
        - `schemaVersion` — always the literal `"pf_compass_plan.v1"`.
        - `summary` — a short narrative of the plan in plain language.
        - `steps[]` — 1-8 `{ title, rationale, suggestedAmount?, currency?, targetDate? }` items, ordered.
        - `confidence` — 0.0-1.0 reflecting data quality honestly (low when `context.guidanceIsPartial`).
        - `reasonCodes` — short machine codes like `"sized_to_safe_to_spend"`, `"partial_guidance"`, `"risk_appetite_aggressive"`.
        - `entities[]` — `{ ref, label }` references the plan touches (e.g. the goal id).
        - `warnings[]` — plain-English notes about missing data or assumptions.
        </output_contract>
        """;

    public AIAgent Build(IChatClient chatClient, IServiceProvider serviceProvider)
        => Build(chatClient, serviceProvider, instructionsOverride: null, allowedToolNames: null);

    public AIAgent Build(
        IChatClient chatClient,
        IServiceProvider serviceProvider,
        string? instructionsOverride,
        IReadOnlySet<string>? allowedToolNames)
    {
        // The planner reasons over the request payload it is handed; it exposes
        // no host tools, so the allowedToolNames filter is a no-op here.
        var instructions = instructionsOverride ?? InstructionsText;

        return new ChatClientAgent(
            chatClient,
            name: Name,
            instructions: instructions,
            tools: []);
    }

    public IReadOnlyList<string> GetToolNames(IServiceProvider serviceProvider) => [];
}
