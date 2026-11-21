using System.Text.Json;

using Aonik.SharedKernel.Abstractions.Connectors;

namespace Aonik.SharedKernel.Abstractions.Workflows;

/// <summary>Authoritative facts for a persisted run actor, never a model-supplied fact replacement.</summary>
public interface IWorkflowFactsProvider
{
    string SubjectType { get; }
    string Schema { get; }
    Task<WorkflowFactsSnapshot?> LoadAsync(Guid subjectId, WorkflowActor actor, CancellationToken cancellationToken = default);
    /// <summary>Domain-authorized recovery references only; never bypasses source access for facts or execution.</summary>
    Task<WorkflowConnectionDependency?> GetConnectionDependencyAsync(Guid subjectId, WorkflowActor actor,
        CancellationToken cancellationToken = default) => Task.FromResult<WorkflowConnectionDependency?>(null);
}

public sealed record WorkflowConnectionDependency(Guid WorkflowId, Guid WorkflowVersionId, Guid BindingId, int BindingRevision,
    string SubjectType, Guid SubjectId, Guid AutomationIdentityId, WorkflowConnectorBinding Connection, string ProviderAccountId);

public sealed record WorkflowActor(string Type, Guid Id, Guid? InitiatingUserId = null, Guid? WorkflowVersionId = null);
public sealed record WorkflowFactsSnapshot(
    string Schema, IReadOnlyDictionary<string, JsonElement> Facts, string SubjectVersion,
    string? BlockReason = null, Guid? SubjectCreatedByUserId = null);
