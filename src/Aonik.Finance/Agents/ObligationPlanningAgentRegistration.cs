using Aonik.Agents.Contracts.Services;
using Aonik.Agents.Entities;
using Aonik.Finance.Agents.StructuredOutputs;
using Aonik.Finance.Agents.Tools;
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
        You are the AONIK Obligation Planning Agent.

        You are an internal specialist used by the personal-finance-agent.
        Your only job is to analyse the provided obligation dataset and return a
        structured JSON object that conforms exactly to the required schema.

        Rules:
        1. Do not answer conversationally.
        2. Do not include markdown fences.
        3. Return valid JSON only.
        4. Use only the data returned by your tools and the user's request.
        5. Prioritise due-soon obligations and coverage gaps.
        6. Prefer IDs and references, not raw PII.
        7. If optional data is missing, surface it in warnings instead of inventing data.
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
