using Aonik.Agents.Contracts.Models;
using Aonik.Agents.Contracts.Services;
using FastEndpoints;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aonik.Agents.Endpoints;

/// <summary>
/// Executes a named workflow (invoice processing, onboarding, reconciliation).
/// Workflows are multi-agent pipelines that coordinate domain-specific agents
/// through sequential or concurrent execution patterns.
///
/// Workflow resolution uses keyed services instead of a switch statement,
/// making it extensible without modifying this endpoint.
/// </summary>
internal sealed class RunWorkflowEndpoint : Endpoint<WorkflowRequest, WorkflowResponse>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RunWorkflowEndpoint> _logger;

    public RunWorkflowEndpoint(IServiceProvider serviceProvider, ILogger<RunWorkflowEndpoint> logger)
    {
        _serviceProvider = serviceProvider;
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
            // Resolve workflow via keyed services (R10).
            // Each workflow factory is registered as a keyed singleton:
            //   services.AddKeyedSingleton<IWorkflowFactory, XxxWorkflowFactory>("workflow-name");
            var factory = _serviceProvider.GetKeyedService<IWorkflowFactory>(
                req.WorkflowName.ToLowerInvariant());

            if (factory is null)
            {
                _logger.LogWarning("Unknown workflow: {WorkflowName}", req.WorkflowName);
                await Send.NotFoundAsync(ct);
                return;
            }

            var workflowAgent = factory.Build(_serviceProvider);

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
}
