using Aonik.Agents.Contracts.Models;
using Aonik.Agents.Workflows;
using FastEndpoints;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Aonik.Agents.Endpoints;

/// <summary>
/// Executes a named workflow (invoice processing, onboarding, reconciliation).
/// Workflows are multi-agent pipelines that coordinate domain-specific agents
/// through sequential or concurrent execution patterns.
/// </summary>
internal sealed class RunWorkflowEndpoint : Endpoint<WorkflowRequest, WorkflowResponse>
{
    private readonly IChatClient _chatClient;
    private readonly ILogger<RunWorkflowEndpoint> _logger;

    public RunWorkflowEndpoint(IChatClient chatClient, ILogger<RunWorkflowEndpoint> logger)
    {
        _chatClient = chatClient;
        _logger = logger;
    }

    public override void Configure()
    {
        Post("/ai/workflows/run");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(WorkflowRequest req, CancellationToken ct)
    {
        _logger.LogInformation("Executing workflow: {WorkflowName}", req.WorkflowName);

        try
        {
            var workflowAgent = ResolveWorkflow(req.WorkflowName);

            if (workflowAgent is null)
            {
                await Send.NotFoundAsync(ct);
                return;
            }

            var response = await workflowAgent.RunAsync(req.Input, cancellationToken: ct);

            await Send.OkAsync(new WorkflowResponse
            {
                WorkflowName = req.WorkflowName,
                Output = response.Text ?? string.Empty,
                Success = true
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Workflow '{WorkflowName}' failed", req.WorkflowName);

            await Send.OkAsync(new WorkflowResponse
            {
                WorkflowName = req.WorkflowName,
                Output = string.Empty,
                Success = false,
                Error = ex.Message
            }, ct);
        }
    }

    private AIAgent? ResolveWorkflow(string workflowName)
    {
        return workflowName.ToLowerInvariant() switch
        {
            "invoice-processing" => InvoiceProcessingWorkflow.Build(_chatClient),
            "tenant-onboarding" => OnboardingWorkflow.Build(_chatClient),
            "financial-reconciliation" => ReconciliationWorkflow.Build(_chatClient),
            _ => null
        };
    }
}
