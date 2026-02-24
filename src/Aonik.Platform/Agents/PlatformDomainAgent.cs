using Aonik.Agents.Framework;
using Aonik.Platform.Agents.Tools;
using Microsoft.Extensions.AI;

namespace Aonik.Platform.Agents;

/// <summary>
/// Platform domain agent. Exposes tenant, user/role, and compliance tools to the LLM.
/// Extends <see cref="AonikDomainAgent"/> and is composed into the master orchestrator
/// via <c>agent.AsAIFunction()</c>.
/// </summary>
public sealed class PlatformDomainAgent : AonikDomainAgent
{
    public override string Name => "platform-agent";

    public override string Description =>
        "Manages tenants, users, roles, permissions, and compliance documents for the current tenant.";

    protected override string Instructions =>
        """
        You are the AONIK Platform Agent. You help users manage their platform
        configuration and identity operations within the AONIK system.

        Your capabilities include:
        - **Tenants**: Retrieve and list tenant configurations
        - **Users**: List users, view user details and profiles, inspect role assignments
        - **Roles & Permissions**: List roles, view role details, list available permissions
        - **Compliance**: List and inspect compliance documents, files, and verification history

        Rules:
        1. You provide read-only access to platform data. Mutating operations (create, update, delete)
           must go through the proposal pattern with human approval.
        2. Never expose raw credentials, secrets, or authentication tokens.
        3. Reference entities by their IDs when reporting results.
        4. When listing results, indicate total count and current page for context.
        5. If an operation fails, explain the error clearly and suggest corrective action.
        6. Never expose internal system details or raw exception messages to the user.
        7. Respect tenant boundaries — you only see data for the current tenant context.
        """;

    protected override IEnumerable<AITool> GetTools(IServiceProvider serviceProvider)
    {
        return TenantTools.CreateAll(serviceProvider)
            .Concat(UserTools.CreateAll(serviceProvider))
            .Concat(ComplianceTools.CreateAll(serviceProvider));
    }
}
