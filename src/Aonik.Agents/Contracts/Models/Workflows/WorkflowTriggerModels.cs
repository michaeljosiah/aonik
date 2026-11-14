using Aonik.SharedKernel.Abstractions.Agents;

namespace Aonik.Agents.Contracts.Models.Workflows;

public sealed record WorkflowTriggerDefinition(int SchemaVersion, string Source, Guid WorkflowVersionId, Guid AutomationIdentityId,
    WorkflowTriggerSubject Subject, WorkflowTriggerLimits Limits, WorkflowTriggerSchedule? Schedule = null, WorkflowTriggerMailbox? Mailbox = null);
public sealed record WorkflowTriggerSubject(string Type, string Mapping, IReadOnlyList<Guid> FixedIds);
public sealed record WorkflowTriggerLimits(int MaximumStartsPerHour = 10, int MaximumConcurrentRuns = 1,
    int MaximumCausationDepth = 4, bool IgnoreSelfGenerated = true);
public sealed record WorkflowTriggerSchedule(string Cron, string TimeZone, DateTime StartAtUtc, DateTime EndAtUtc,
    int MaximumOccurrences, int MaximumCatchUpOccurrences = 0);
public sealed record WorkflowTriggerMailbox(Guid ConnectionId, Guid WorkflowGrantId,
    IReadOnlyList<string> LabelIds, string Mode, int PollIntervalMinutes, bool AllowPollingFallback = false,
    DateTime? BackfillSinceUtc = null);
public sealed record SaveWorkflowTriggerRequest(string Name, WorkflowTriggerDefinition Definition, string? ExpectedVersion = null);
public sealed record WorkflowTriggerCommand(string ExpectedVersion, string DefinitionHash);
public sealed record WorkflowTriggerDetail(Guid Id, Guid WorkflowId, string Name, string Status, int DraftRevision,
    string DraftHash, WorkflowTriggerDefinition Draft, int? ActiveRevision, string? ActiveHash,
    WorkflowTriggerDefinition? Active, Guid? ActivationProposalId, Guid? TaskId, Guid? SubscriptionId,
    long AdmittedRuns, string? LastError, string ExpectedVersion);
public sealed record WorkflowTriggerValidation(string DefinitionHash, IReadOnlyList<WorkflowDiagnostic> Diagnostics);
public sealed record WorkflowTriggerGovernanceResult(Guid ProposalId, Guid ApprovalRequestId, ToolApprovalPreview Preview);
internal sealed record WorkflowTriggerSource(Guid BindingId, int Revision, Guid ApprovalProposalId, string DefinitionHash,
    Guid SourceEventId, DateTime OccurredAtUtc, Guid? CausationRunId, bool SelfGenerated = false);
