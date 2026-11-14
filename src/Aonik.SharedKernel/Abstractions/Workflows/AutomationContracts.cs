namespace Aonik.SharedKernel.Abstractions.Workflows;

public sealed record AutomationActionRef(string Key, int Version);
public sealed record AutomationWorkflowGrant(Guid WorkflowVersionId, string SubjectType, bool AllSubjects,
    IReadOnlyList<Guid> SubjectIds, IReadOnlyList<AutomationActionRef> Actions);
public sealed record AutomationIdentityCommand(string Name, Guid AccountableUserId, DateTime? ExpiresAt,
    IReadOnlyList<AutomationWorkflowGrant> Grants, string? ExpectedVersion = null);
public sealed record AutomationIdentityDetail(Guid Id, string Name, Guid AccountableUserId, string Status,
    DateTime? ExpiresAt, IReadOnlyList<AutomationWorkflowGrant> Grants, string ExpectedVersion, Guid? ConfiguredByUserId);
public sealed record AutomationActionCapability(string Key, int Version, string RequiredPermission);
public sealed record AutomationWorkflowCapability(Guid WorkflowId, Guid WorkflowVersionId, string DefinitionHash,
    IReadOnlyList<AutomationActionCapability> Actions);
public sealed record AutomationIdentityUsage(int ActiveRuns, IReadOnlyList<Guid> WorkflowVersionIds);

/// <summary>Platform-owned authority for an internal actor. It never creates an authenticated human context.</summary>
public interface IAutomationIdentityAuthorization
{
    /// <returns>The configuring human's ID for audit correlation only; it grants no human authority.</returns>
    Task<Guid> RequireAsync(WorkflowActor actor, string permission, string? subjectType = null, Guid? subjectId = null,
        AutomationActionRef? action = null, CancellationToken cancellationToken = default);
    Task<bool> IsActiveAsync(Guid identityId, CancellationToken cancellationToken = default);
}

/// <summary>Agents validates immutable workflow capabilities before Platform grants them to a machine actor.</summary>
public interface IWorkflowAutomationDefinitionReader
{
    Task<AutomationWorkflowCapability> RequireAsync(Guid workflowVersionId, Guid configuringUserId, CancellationToken cancellationToken = default);
    Task<AutomationIdentityUsage> GetUsageAsync(Guid identityId, CancellationToken cancellationToken = default);
}

/// <summary>Only trusted trigger/worker services call admission; no public actor-ID login or token endpoint.</summary>
public interface IWorkflowAutomationAdmission
{
    Task<Guid> AdmitAsync(Guid identityId, Guid workflowVersionId, string subjectType, Guid subjectId,
        string idempotencyKey, CancellationToken cancellationToken = default);
}
