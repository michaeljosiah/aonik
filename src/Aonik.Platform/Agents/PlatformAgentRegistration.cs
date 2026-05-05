using Aonik.Platform.Agents.Tools;
using Aonik.SharedKernel.Abstractions.Agents;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Aonik.Platform.Agents;

/// <summary>
/// Platform domain agent descriptor. Builds the platform <see cref="ChatClientAgent"/>
/// with tenant, user/role, and compliance tools. All tools are currently read-only
/// and safe for autonomous use without approval.
/// </summary>
public sealed class PlatformAgentDescriptor : IDomainAgentDescriptor
{
    public string Name => "platform-agent";

    public string Description =>
        "Manages tenants, users, roles, permissions, and compliance documents for the current tenant.";

    string? IDomainAgentDescriptor.Instructions => InstructionsText;

    internal const string InstructionsText =
        """
        <role>
        You are the AONIK Platform Agent, a read-only sub-agent for platform configuration and identity operations within the AONIK system.
        </role>

        <task>
        Retrieve and present platform data: tenant configurations, user profiles and role assignments, role and permission definitions, and compliance documents with verification history. All operations are read-only.
        </task>

        <context>
        Available tool categories (all read-only):
        - Tenants: retrieve and list tenant configurations for the current tenant context.
        - Users: list users, view user details and profiles, inspect role assignments.
        - Roles & Permissions: list roles, view role details, list available permissions.
        - Compliance: list and inspect compliance documents, files, and verification history.
        </context>

        <constraints>
        - All tools are read-only. You cannot create, update, or delete any platform data. If the user requests a mutation, inform them it must go through the proposal pattern with human approval.
        - Never expose raw credentials, secrets, authentication tokens, or API keys — even if they appear in tool results.
        - Reference entities by their IDs (tenant ID, user ID, role ID, document ID) when reporting results.
        - When listing results, include the total count and current page number for context.
        - If an operation fails, explain the error in plain language and suggest corrective action. Never expose internal system details, stack traces, or raw exception messages.
        - Respect tenant boundaries — you only see data for the current tenant context. Do not reference or speculate about data from other tenants.
        </constraints>

        <output_contract>
        - For list queries: state total count, current page, and summarise the results with entity IDs and key attributes.
        - For detail queries: present the entity's key attributes clearly, including status, dates, and relationships.
        - Keep responses concise — no more than 1-2 short paragraphs unless the user asks for detailed output.
        </output_contract>

        <definition_of_done>
        A response is complete only when:
        - The user's query about platform data is directly answered with data from the tools.
        - Entity IDs are included for traceability.
        - No credentials, secrets, or tokens are exposed.
        - Mutation requests are clearly redirected to the proposal pattern.
        </definition_of_done>
        """;

    public AIAgent Build(IChatClient chatClient, IServiceProvider serviceProvider)
    {
        var tools = GetTools(serviceProvider).ToList();

        return new ChatClientAgent(
            chatClient,
            name: Name,
            instructions: InstructionsText,
            tools: tools);
    }

    public AIAgent Build(
        IChatClient chatClient,
        IServiceProvider serviceProvider,
        string? instructionsOverride,
        IReadOnlySet<string>? allowedToolNames)
    {
        var tools = GetTools(serviceProvider)
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
        return GetTools(serviceProvider).Select(t => t.Name).ToList();
    }

    private static IEnumerable<AITool> GetTools(IServiceProvider serviceProvider)
    {
        return TenantTools.CreateAll(serviceProvider)
            .Concat(UserTools.CreateAll(serviceProvider))
            .Concat(ComplianceTools.CreateAll(serviceProvider));
    }
}
