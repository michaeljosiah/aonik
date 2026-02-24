namespace Aonik.Agents.Contracts.Models;

/// <summary>
/// Request model for triggering a named workflow.
/// </summary>
public sealed record WorkflowRequest
{
    /// <summary>The workflow to execute (e.g., "invoice-processing", "tenant-onboarding", "financial-reconciliation").</summary>
    public required string WorkflowName { get; init; }

    /// <summary>The input message / instructions for the workflow.</summary>
    public required string Input { get; init; }
}

/// <summary>
/// Response from a workflow execution.
/// </summary>
public sealed record WorkflowResponse
{
    /// <summary>The workflow that was executed.</summary>
    public required string WorkflowName { get; init; }

    /// <summary>The aggregated output from the workflow agents.</summary>
    public required string Output { get; init; }

    /// <summary>Whether the workflow completed successfully.</summary>
    public required bool Success { get; init; }

    /// <summary>Error message if the workflow failed.</summary>
    public string? Error { get; init; }
}
