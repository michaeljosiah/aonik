using Aonik.SharedKernel.Abstractions.Connectors;

namespace Aonik.Agents.Contracts.Models.Tenant;

public sealed record ImportConnectorPackageRequest(string PackageKey, string PackageVersion, string Digest, string? AuthSecret = null, string? AuthUsername = null)
{
    public override string ToString() => $"ImportConnectorPackage({PackageKey}@{PackageVersion}, credentials redacted)";
}
public sealed record ConnectorPackageInstallationCommand(string ExpectedVersion, string? Notes = null);
public sealed record ReviewConnectorPackageInstallationRequest(string ExpectedVersion, bool Approve, string? Notes = null);
public sealed record RotateConnectorPackageCredentialRequest(string ExpectedVersion, string AuthSecret, string? AuthUsername = null)
{
    public override string ToString() => "RotateConnectorPackageCredential(<private>)";
}
public sealed record ConnectorPackageInstallationItem(Guid Id, string AdapterKind, string Name, string PackageKey, string PackageVersion,
    string Digest, ConnectorActionRef? Action, string ApprovalState, bool IsActive, bool AuthConfigured, int CredentialVersion, string ExpectedVersion);
public sealed record ConnectorPackageWorkflowUsage(Guid WorkflowId, Guid WorkflowVersionId, string Slug, string Version, bool Current);
public sealed record ConnectorPackageRunUsage(Guid RunId, Guid WorkflowVersionId, string Status);
public sealed record ConnectorPackageReviewedAction(ConnectorActionRef Action, string Tier, IReadOnlyList<string> Scopes, string ContractHash);
public sealed record ConnectorPackageImpact(ConnectorPackageInstallationItem Installation, ConnectorPackageSummary? Target,
    IReadOnlyList<ConnectorPackageReviewedAction> CurrentActions, IReadOnlyList<ConnectorPackageReviewedAction> TargetActions,
    IReadOnlyList<ConnectorPackageWorkflowUsage> Workflows, IReadOnlyList<ConnectorPackageRunUsage> Runs, bool Truncated);
