using Aonik.SharedKernel.Abstractions.Connectors;

namespace Aonik.Agents.Contracts.Models.Tenant;

public sealed record ImportConnectorPackageRequest(string PackageKey, string PackageVersion, string Digest, string? AuthSecret = null, string? AuthUsername = null)
{
    public override string ToString() => $"ImportConnectorPackage({PackageKey}@{PackageVersion}, credentials redacted)";
}
public sealed record ConnectorPackageInstallationCommand(string ExpectedVersion, string? Notes = null);
public sealed record ReviewConnectorPackageInstallationRequest(string ExpectedVersion, bool Approve, string? Notes = null);
public sealed record ConnectorPackageInstallationItem(Guid Id, string AdapterKind, string Name, string PackageKey, string PackageVersion,
    string Digest, ConnectorActionRef? Action, string ApprovalState, bool IsActive, bool AuthConfigured, int CredentialVersion, string ExpectedVersion);
