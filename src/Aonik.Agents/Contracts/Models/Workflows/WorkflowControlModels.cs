namespace Aonik.Agents.Contracts.Models.Workflows;

public sealed record WorkflowWaitParameters(int SchemaVersion, int? DelaySeconds = null, DateTime? UntilUtc = null);

public sealed record WorkflowHumanParameters(int SchemaVersion, string Purpose, WorkflowHumanReviewer Reviewer,
    int TimeoutSeconds, IReadOnlyList<WorkflowHumanChoice> Choices, string RejectRoute, string TimeoutRoute);
public sealed record WorkflowHumanReviewer(Guid? UserId = null, Guid? RoleId = null, bool ForbidSelfApproval = true);
public sealed record WorkflowHumanChoice(string Key, string Label, string Route);

public sealed record WorkflowVersionPin(Guid VersionId, string DefinitionHash);
public sealed record WorkflowChildParameters(int SchemaVersion, WorkflowVersionPin Workflow, WorkflowInputBinding InvoiceId,
    string CompletedRoute = "completed", string ErrorRoute = "error");
public sealed record WorkflowLoopItem(string Key, Guid InvoiceId);
public sealed record WorkflowLoopParameters(int SchemaVersion, WorkflowVersionPin Body, IReadOnlyList<WorkflowLoopItem> Items,
    int MaxIterations, int TimeoutSeconds, string CompletedRoute = "completed", string ExhaustedRoute = "exhausted", string ErrorRoute = "error",
    string? ItemsFromDecision = null);
public sealed record WorkflowParallelBranch(string Key, WorkflowVersionPin Workflow, WorkflowInputBinding InvoiceId);
public sealed record WorkflowParallelParameters(int SchemaVersion, IReadOnlyList<WorkflowParallelBranch> Branches,
    int MaxConcurrency, string JoinPolicy, string CompletedRoute = "completed", string ErrorRoute = "error");

internal static class WorkflowControlLimits
{
    public const int MaximumGraphNodes = 100;
    public const int MaximumRootInvocations = 1000;
    public const int MaximumLoopIterations = 100;
    public const int MaximumBranches = 4;
    public const int MaximumNestingDepth = 5;
    public const int MaximumLifetimeSeconds = 7 * 24 * 60 * 60;
}
