using Microsoft.EntityFrameworkCore;

using Aonik.Agents.Entities;
using Aonik.Agents.Framework;
using Aonik.Agents.Persistence;
using Aonik.SharedKernel.Abstractions.Connectors;
using Aonik.SharedKernel.Abstractions.Multitenancy;

namespace Aonik.Agents.Services.Connectors.Catalog;

/// <summary>Uses current tenant rows without an HTTP principal or copied service credentials.</summary>
internal sealed class ConnectorPackageAccountReader(AgentsDbContext database, CuratedConnectorCatalog catalog, ITenantProvider tenant,
    ITenantCredentialProtector protector, ITenantEgressAllowList egress)
{
    public async Task<ConnectorPackageAccount> RequireAsync(string connectorKey, string accountId, ConnectorActionRef? action = null,
        ConnectorArtifactPin? artifact = null, CancellationToken cancellationToken = default)
    {
        string[] parts = accountId?.Split(':') ?? [];
        if (parts.Length != 2 || parts[0] is not ("mcp" or "http") || !Guid.TryParseExact(parts[1], "N", out Guid id)) throw Denied();
        Guid tenantId = tenant.GetCurrentTenantId();
        TenantMcpServer? server = null; TenantHttpTool? tool = null;
        VerifiedConnectorPackage package; ConnectorPackageAction? selected = null;
        try
        {
            if (parts[0] == "mcp")
            {
                server = await database.TenantMcpServers.AsNoTracking().SingleOrDefaultAsync(row => row.TenantId == tenantId && row.Id == id
                    && row.ConnectorPackageJson != null && row.IsActive && row.ApprovalState == TenantExtensionApprovalState.Approved, cancellationToken) ?? throw Denied();
                package = ConnectorPackageReferences.Require(catalog, server, tenantId);
                if (server.ReviewedByUserId is null || server.ReviewedAt is null) throw Denied();
                _ = TenantRemoteAuth.RequireHeaders(server.AuthKind, server.ProtectedAuthJson, protector, package.Manifest.Authentication.HeaderName);
            }
            else
            {
                tool = await database.TenantHttpTools.AsNoTracking().SingleOrDefaultAsync(row => row.TenantId == tenantId && row.Id == id
                    && row.ConnectorPackageJson != null && row.IsActive && row.ApprovalState == TenantExtensionApprovalState.Approved, cancellationToken) ?? throw Denied();
                (package, selected) = ConnectorPackageReferences.Require(catalog, tool, tenantId);
                if (tool.ReviewedByUserId is null || tool.ReviewedAt is null) throw Denied();
                _ = TenantRemoteAuth.RequireHeaders(tool.AuthKind, tool.ProtectedAuthJson, protector, package.Manifest.Authentication.HeaderName);
                if (tool.RiskTier != (selected.MinimumRiskTier == "ReadOnly" ? TenantToolRiskTier.ReadOnly : TenantToolRiskTier.High)) throw Denied();
            }
        }
        catch (Exception error) when (error is InvalidOperationException or System.Text.Json.JsonException)
        { throw Denied(); }
        if (package.Manifest.ConnectorKey != connectorKey) throw Denied();
        ConnectorPackageAction[] allowed = selected is not null ? [selected] : package.Manifest.Actions.ToArray();
        if (allowed.Any(item => !egress.IsAllowed(item.Endpoint, out _))) throw new ConnectorAccessException("EndpointBlocked", "The reviewed endpoint is no longer allowed.");
        if (action is not null)
        {
            ConnectorPackageAction exact = allowed.SingleOrDefault(item => item.Key == action.Key && item.Version == action.Version) ?? throw Denied();
            if (artifact is not null && artifact != package.Pin(exact)) throw new ConnectorAccessException("ConnectorContractChanged", "The workflow artifact pin does not match the installed release.");
        }
        else if (artifact is not null) throw Denied();
        return new(package, server, tool, allowed);
    }

    private static ConnectorAccessException Denied() => new("ConnectorUnavailable", "The exact tenant service account requires an active, unchanged, platform-reviewed installation and valid credentials.");
}

internal sealed record ConnectorPackageAccount(VerifiedConnectorPackage Package, TenantMcpServer? Mcp, TenantHttpTool? Http,
    IReadOnlyList<ConnectorPackageAction> Actions)
{
    public ConnectorServiceAccount Describe() => new(Package.Manifest.ConnectorKey,
        Mcp is not null ? $"mcp:{Mcp.Id:N}" : $"http:{Http!.Id:N}", Package.Manifest.DisplayName,
        Mcp?.CredentialVersion ?? Http!.CredentialVersion,
        Actions.Select(action => new ConnectorServiceAccountAction(new(action.Key, action.Version), Package.Pin(action))).ToArray());
    public override string ToString() => $"ConnectorPackageAccount({Package.Manifest.PackageKey}@{Package.Manifest.PackageVersion})";
}
