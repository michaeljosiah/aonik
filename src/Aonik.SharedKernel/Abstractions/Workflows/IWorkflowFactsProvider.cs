using System.Text.Json;

namespace Aonik.SharedKernel.Abstractions.Workflows;

/// <summary>Authoritative facts for a persisted run actor, never a model-supplied fact replacement.</summary>
public interface IWorkflowFactsProvider
{
    string SubjectType { get; }
    string Schema { get; }
    Task<WorkflowFactsSnapshot?> LoadAsync(Guid subjectId, WorkflowActor actor, CancellationToken cancellationToken = default);
}

public sealed record WorkflowActor(string Type, Guid Id, Guid? InitiatingUserId = null, Guid? WorkflowVersionId = null);
public sealed record WorkflowFactsSnapshot(
    string Schema, IReadOnlyDictionary<string, JsonElement> Facts, string SubjectVersion,
    string? BlockReason = null, Guid? SubjectCreatedByUserId = null);
