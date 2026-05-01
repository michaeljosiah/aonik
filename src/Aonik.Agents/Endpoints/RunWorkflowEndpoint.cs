using Aonik.Agents.Contracts.Models;
using Aonik.Agents.Contracts.Services;
using Aonik.Agents.Workflows.Graph;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
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
        Summary(s =>
        {
            s.Summary = "Execute a named workflow";
            s.Description = "Runs a multi-agent workflow pipeline by name. Workflows coordinate domain-specific agents through sequential or concurrent execution patterns.";
            s.Response(200, "Success");
            s.Response(400, "Invalid request");
            s.Response(401, "Not authenticated");
            s.Response(404, "Workflow not found");
        });
        Options(x => x.WithTags("AI Agents"));
    }

    public override async Task HandleAsync(WorkflowRequest req, CancellationToken ct)
    {
        _logger.LogInformation("Executing workflow: {WorkflowName}", req.WorkflowName);

        try
        {
            // Resolve workflow via keyed services (R10).
            // Each workflow factory is registered as a keyed singleton:
            //   services.AddKeyedSingleton<IWorkflowFactory, XxxWorkflowFactory>("workflow-name");
            //
            // If no legacy keyed factory matches, fall back to the generic
            // graph-driven factory which loads the workflow's saved
            // nodes + edges and translates them into a MAF Workflow at
            // run time. This is how editor-saved workflows execute.
            var factory = _serviceProvider.GetKeyedService<IWorkflowFactory>(
                req.WorkflowName.ToLowerInvariant());

            if (factory is null)
            {
                var graphProvider = _serviceProvider.GetRequiredService<IGraphWorkflowFactoryProvider>();
                factory = graphProvider.For(req.WorkflowName);
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
