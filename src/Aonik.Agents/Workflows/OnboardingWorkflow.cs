using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace Aonik.Agents.Workflows;

/// <summary>
/// Tenant onboarding workflow using MAF sequential pipeline pattern.
/// Steps:
/// 1. Validation Agent — verifies onboarding data (tenant name, admin user, config)
/// 2. Provisioning Agent — creates tenant, sets up roles, seeds reference data
/// 3. Verification Agent — confirms everything is set up and ready
///
/// This workflow coordinates the multi-step tenant onboarding process that
/// currently requires multiple manual API calls.
/// </summary>
public static class OnboardingWorkflow
{
    public const string WorkflowName = "tenant-onboarding";

    /// <summary>
    /// Builds the sequential onboarding workflow as an <see cref="AIAgent"/>.
    /// </summary>
    public static AIAgent Build(IChatClient chatClient)
    {
        var validationAgent = new ChatClientAgent(
            chatClient,
            name: "onboarding-validator",
            instructions:
                """
                You are a tenant onboarding validation specialist. When given a tenant setup request,
                verify that all required information is present:
                - Tenant name must be provided and non-empty
                - Admin user email must be a valid email address
                - Base currency must be a valid ISO 4217 code (default: USD)
                - Country must be a valid ISO 3166-1 alpha-2 code

                If validation passes, respond with "VALIDATED" followed by a summary.
                If validation fails, list all issues that need to be corrected.
                """);

        var provisioningAgent = new ChatClientAgent(
            chatClient,
            name: "onboarding-provisioner",
            instructions:
                """
                You are a tenant provisioning agent. When you receive a validated onboarding request,
                describe the provisioning steps that would be executed:

                1. Create the tenant record with the provided name and configuration
                2. Set up default roles (Admin, Manager, User) with standard permissions
                3. Create the admin user account with the provided email
                4. Assign the Admin role to the admin user
                5. Seed reference data (countries, currencies, notification templates)
                6. Create a default ledger in the base currency

                Confirm each step and note any issues encountered.
                """);

        var verificationAgent = new ChatClientAgent(
            chatClient,
            name: "onboarding-verifier",
            instructions:
                """
                You are an onboarding verification agent. Review the provisioning results
                and produce a final onboarding report:
                - Confirm the tenant was created successfully
                - Confirm the admin user has access
                - List any warnings or items requiring manual follow-up
                - Provide the tenant ID and admin user ID for reference
                - Suggest next steps (e.g., "configure authentication provider", "invite team members")
                """);

        var workflow = AgentWorkflowBuilder.BuildSequential(
            WorkflowName,
            [validationAgent, provisioningAgent, verificationAgent]);

        return workflow.AsAIAgent(
            id: WorkflowName,
            name: "Tenant Onboarding Pipeline",
            description: "Validates, provisions, and verifies new tenant setup through a multi-step pipeline");
    }
}
