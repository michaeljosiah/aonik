using Aonik.Agents.Contracts.Models;
using Aonik.Agents.Contracts.Services;
using Aonik.Agents.Services.Workflows;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aonik.Agents.Endpoints;

/// <summary>Runs explicitly namespaced legacy factories; saved graphs use the published-version run path.</summary>
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
        Summary(s =>
        {
            s.Summary = "Execute an explicitly named legacy factory";
            s.Description = "Legacy factory names require the legacy: prefix. Saved workflow drafts cannot execute through this endpoint.";
            s.Response(200, "Success");
            s.Response(400, "Invalid request");
            s.Response(401, "Not authenticated");
            s.Response(404, "Workflow not found");
        });
        Options(x => x.WithTags("AI Agents"));
    }

    public override async Task HandleAsync(WorkflowRequest req, CancellationToken ct)
    {
        const string prefix = "legacy:";
        if (!req.WorkflowName.StartsWith(prefix, StringComparison.Ordinal))
        {
            await Send.ResultAsync(Results.Conflict(new
            {
                code = "workflow.publication_required",
                error = "Saved workflows require explicit publication and the versioned run endpoint. Legacy factory names require the legacy: prefix.",
            }));
            return;
        }
        await _serviceProvider.GetRequiredService<WorkflowAuthorization>().RequireAsync("Workflow.Run", ct);
        IWorkflowFactory? factory = _serviceProvider.GetKeyedService<IWorkflowFactory>(req.WorkflowName[prefix.Length..].ToLowerInvariant());
        if (factory is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        try
        {
            var workflowAgent = factory.Build(_serviceProvider);
            var response = await workflowAgent.RunAsync(req.Input, cancellationToken: ct);
            await Send.OkAsync(new WorkflowResponse
            {
                WorkflowName = req.WorkflowName,
                Output = response.Text ?? string.Empty,
                Success = true,
            }, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Legacy workflow {WorkflowName} failed", req.WorkflowName);
            await Send.ResultAsync(Results.Problem("Legacy workflow execution failed.", statusCode: 422));
        }
    }
}
