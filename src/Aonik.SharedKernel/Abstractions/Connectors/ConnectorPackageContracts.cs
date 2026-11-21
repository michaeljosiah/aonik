using System.Text.Json;

namespace Aonik.SharedKernel.Abstractions.Connectors;

/// <summary>A passive, signed descriptor artifact. It contains no credentials, executable code or installation scripts.</summary>
public sealed record ConnectorPackageArtifact(int SchemaVersion, string PublisherId, string KeyId,
    string PayloadBase64, string Sha256, string SignatureBase64);

public sealed record ConnectorPackageManifest(int SchemaVersion, string PublisherId, string PackageKey, string PackageVersion,
    string ConnectorKey, string DisplayName, string AdapterKind, int MinimumHostContractVersion, int MaximumHostContractVersion,
    ConnectorPackageAuthentication Authentication, ConnectorPackageLimits Limits, IReadOnlyList<ConnectorPackageAction> Actions);

public sealed record ConnectorPackageAuthentication(string Kind, string? HeaderName = null);
public sealed record ConnectorPackageLimits(int MaximumInputBytes = 65_536, int MaximumOutputBytes = 65_536, int TimeoutSeconds = 30);
public sealed record ConnectorPackageAction(string Key, int Version, string DisplayName, string MinimumRiskTier,
    JsonElement InputSchema, JsonElement OutputSchema, IReadOnlyList<string> RequiredScopes, string Endpoint,
    string? RemoteToolName = null, string? HttpMethod = null, IReadOnlyList<IReadOnlyList<string>>? ScopeAlternatives = null);

/// <summary>Exact release identity retained with a remote workflow binding; upgrades do not reinterpret it.</summary>
public sealed record ConnectorArtifactPin(string PackageKey, string PackageVersion, string ArtifactDigest, string ActionContractHash);
public sealed record ConnectorPackageInstallationRef(string PackageKey, string PackageVersion, string Digest, ConnectorActionRef? Action = null);

public sealed record ConnectorPackageSummary(string PackageKey, string PackageVersion, string ConnectorKey, string DisplayName,
    string PublisherId, string Digest, string AdapterKind, int ActionCount);
public sealed record ConnectorPackageDetail(ConnectorPackageSummary Package, ConnectorPackageManifest Manifest);

public interface IConnectorPackageCatalog
{
    Task<IReadOnlyList<ConnectorPackageSummary>> ListAsync(CancellationToken cancellationToken = default);
    Task<ConnectorPackageDetail?> GetAsync(string packageKey, string version, CancellationToken cancellationToken = default);
    Task<string?> DownloadAsync(string packageKey, string version, CancellationToken cancellationToken = default);
}

public interface INativeConnectorReleaseCatalog
{
    ConnectorArtifactPin? Latest(string connectorKey, ConnectorActionRef action);
    void Require(string connectorKey, ConnectorActionRef action, ConnectorArtifactPin pin);
}
