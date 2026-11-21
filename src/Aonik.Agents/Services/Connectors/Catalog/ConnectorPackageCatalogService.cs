using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Connectors;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Platform;

namespace Aonik.Agents.Services.Connectors.Catalog;

internal sealed class ConnectorPackageCatalogService(CuratedConnectorCatalog catalog, ITenantProvider tenantProvider,
    ICurrentUserProvider currentUser, IUserDirectoryReader users, IPermissionService permissions) : IConnectorPackageCatalog
{
    public async Task<IReadOnlyList<ConnectorPackageSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        await RequireReaderAsync(cancellationToken);
        return catalog.Packages.Select(Summary).ToArray();
    }

    public async Task<ConnectorPackageDetail?> GetAsync(string packageKey, string version, CancellationToken cancellationToken = default)
    {
        await RequireReaderAsync(cancellationToken);
        VerifiedConnectorPackage? package = Find(packageKey, version);
        return package is null ? null : new(Summary(package), package.Manifest);
    }

    public async Task<string?> DownloadAsync(string packageKey, string version, CancellationToken cancellationToken = default)
    {
        await RequireReaderAsync(cancellationToken);
        return Find(packageKey, version)?.ArtifactJson;
    }

    private VerifiedConnectorPackage? Find(string packageKey, string version)
        => catalog.Packages.SingleOrDefault(package => package.Manifest.PackageKey == packageKey && package.Manifest.PackageVersion == version);
    private async Task RequireReaderAsync(CancellationToken cancellationToken)
    {
        if (!tenantProvider.TryGetCurrentTenantId(out Guid tenantId) || tenantId == Guid.Empty || currentUser.GetCurrentUserId() is not { } userId || userId == Guid.Empty
            || !await permissions.HasPermissionAsync(userId, "Integration.Read", cancellationToken)
            || !(await users.GetByIdsAsync(tenantId, [userId], cancellationToken)).Any(user => user.UserId == userId && user.Status == "Active"))
            throw new PermissionDeniedException("Integration.Read");
    }
    internal static ConnectorPackageSummary Summary(VerifiedConnectorPackage package) => new(package.Manifest.PackageKey, package.Manifest.PackageVersion,
        package.Manifest.ConnectorKey, package.Manifest.DisplayName, package.Manifest.PublisherId, package.Digest, package.Manifest.AdapterKind, package.Manifest.Actions.Count);
}
