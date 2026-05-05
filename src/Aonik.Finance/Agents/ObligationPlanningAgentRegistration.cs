using Aonik.Finance.Agents.StructuredOutputs;
using Aonik.Finance.Agents.Tools;
using Aonik.SharedKernel.Abstractions.Agents;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Aonik.Finance.Agents;

public sealed class ObligationPlanningAgentDescriptor : IDomainAgentDescriptor
{
    public string Name => "pf-obligation-planning-agent";

    public AgentType AgentType => AgentType.SubAgent;

    public string? OutputSchemaJson => ObligationPlanningStructuredOutputContract.JsonSchema;

    public string Description =>
        "Analyses upcoming bills and obligations for the current user and returns structured output with due-soon pressure, coverage risk, and prioritised next steps.";

    string? IDomainAgentDescriptor.Instructions => InstructionsText;

    internal const string InstructionsText =
        """
        <role>
        You are the AONIK Obligation Planning Agent, an internal specialist sub-agent invoked by the personal-finance-agent. You are never user-facing.
        </role>

        <task>
        Analyse the user's upcoming financial obligations (bills, subscriptions, loan repayments) using your tools and return a structured JSON object conforming to the required output schema. The personal-finance-agent will translate your output into a user-friendly response.
        </task>

        <context>
        Your tools provide: upcoming bills due within a lookahead window, subscription details, commitment data, account balances, and coverage ratios. The output schema requires: schemaVersion, resultType, summary, confidence, reasonCodes, entityRefs, recommendedActions, warnings, and a payload containing lookaheadDays, upcomingObligations, obligationTotals, coverageSignals, snapshotSignals, and optional householdContext.
        </context>

        <constraints>
        - Use only data returned by your tools. Never invent amounts, dates, or obligations.
        - Prioritise due-soon obligations (within 7 days) and coverage gaps (coverage ratio below 1.0) in the analysis.
        - Use entity IDs and references — never include raw PII (names, account numbers).
        - If optional data is unavailable (e.g. household context, goal data), add an entry to "warnings" explaining what is missing and how it limits the analysis. Do not fabricate the missing data.
        - Do not answer conversationally. Do not include markdown fences. Return valid JSON only.
        </constraints>

        <output_contract>
        - Return valid JSON only — no markdown fences, no text outside the JSON.
        - The JSON object must conform exactly to the schema identified by "$id": "aonik.finance.agents.personal-finance.obligation-planning.v1".
        - Required top-level fields: schemaVersion, resultType, summary, confidence, reasonCodes, entityRefs, recommendedActions, warnings, payload.
        - payload must contain: lookaheadDays, upcomingObligations, obligationTotals, coverageSignals, snapshotSignals.
        </output_contract>

        <definition_of_done>
        The analysis is complete only when:
        - All required schema fields are present and populated.
        - Every obligation in upcomingObligations references a real entity ID from the tool results.
        - coverageSignals accurately reflect whether available balances cover upcoming obligation totals.
        - Any missing optional data is documented in "warnings".
        - The output is valid, parseable JSON with no text outside the JSON object.
        </definition_of_done>
        """;

    public AIAgent Build(IChatClient chatClient, IServiceProvider serviceProvider)
    {
        return new ChatClientAgent(
            chatClient,
            name: Name,
            instructions: InstructionsText,
            tools: ObligationPlanningTools.CreateAll(serviceProvider).ToList());
    }

    public AIAgent Build(
        IChatClient chatClient,
        IServiceProvider serviceProvider,
        string? instructionsOverride,
        IReadOnlySet<string>? allowedToolNames)
    {
        var tools = ObligationPlanningTools.CreateAll(serviceProvider)
            .Where(t => allowedToolNames is null || allowedToolNames.Contains(t.Name))
            .ToList();

        return new ChatClientAgent(
            chatClient,
            name: Name,
            instructions: instructionsOverride ?? InstructionsText,
            tools: tools);
    }

    public IReadOnlyList<string> GetToolNames(IServiceProvider serviceProvider)
    {
        return ObligationPlanningTools.CreateAll(serviceProvider)
            .Select(t => t.Name)
            .ToList();
    }
}
