using Aonik.Agents.Contracts.Services;
using Aonik.Agents.Entities;
using Aonik.Finance.Agents.StructuredOutputs;
using Aonik.Finance.Agents.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Aonik.Finance.Agents;

public sealed class SpendingIntelligenceAgentDescriptor : IDomainAgentDescriptor
{
    public string Name => "pf-spending-intelligence-agent";

    public AgentType AgentType => AgentType.SubAgent;

    public string? OutputSchemaJson => SpendingIntelligenceStructuredOutputContract.JsonSchema;

    public string Description =>
        "Analyses spending behaviour for a given period and returns structured output with category pressure, merchant patterns, budget stress, and snapshot-backed insight signals.";

    string? IDomainAgentDescriptor.Instructions => InstructionsText;

    internal const string InstructionsText =
        """
        You are the AONIK Spending Intelligence Agent.

        You are an internal specialist used by the personal-finance-agent.
        Your only job is to analyse the provided spending dataset and return a
        structured JSON object that conforms exactly to the required schema.

        Rules:
        1. Do not answer conversationally.
        2. Do not include markdown fences.
        3. Return valid JSON only.
        4. Use only the data returned by your tools and the user's request.
        5. Keep summaries concise and evidence-based.
        6. Prefer IDs and references, not raw PII.
        7. If some optional data is missing, reflect that in warnings instead of inventing data.
        """;

    public AIAgent Build(IChatClient chatClient, IServiceProvider serviceProvider)
    {
        return new ChatClientAgent(
            chatClient,
            name: Name,
            instructions: InstructionsText,
            tools: SpendingIntelligenceTools.CreateAll(serviceProvider).ToList());
    }

    public AIAgent Build(
        IChatClient chatClient,
        IServiceProvider serviceProvider,
        string? instructionsOverride,
        IReadOnlySet<string>? allowedToolNames)
    {
        var tools = SpendingIntelligenceTools.CreateAll(serviceProvider)
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
        return SpendingIntelligenceTools.CreateAll(serviceProvider)
            .Select(t => t.Name)
            .ToList();
    }
}
