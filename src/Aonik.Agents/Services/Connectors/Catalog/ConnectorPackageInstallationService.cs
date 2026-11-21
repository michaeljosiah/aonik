using System.Text.Json;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

using Aonik.Agents.Contracts.Models.Tenant;
using Aonik.Agents.Contracts.Services;
using Aonik.Agents.Entities;
using Aonik.Agents.Framework;
using Aonik.Agents.Persistence;
using Aonik.Agents.Services.Tenant;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Connectors;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Platform;
using Aonik.SharedKernel.Primitives;
using Aonik.SharedKernel.Serialization;

namespace Aonik.Agents.Services.Connectors.Catalog;

/// <summary>Imports passive releases into the existing tenant extension review lifecycle. No executable installation or account grant occurs.</summary>
internal sealed partial class ConnectorPackageInstallationService(AgentsDbContext database, CuratedConnectorCatalog catalog,
    TenantMcpServerService mcp, TenantHttpToolService http, ITenantEgressAllowList egress, ITenantProvider tenantProvider,
    ICurrentUserProvider currentUser, IUserDirectoryReader users, IPermissionService permissions,
    IAuthorizationService authorization, IHttpContextAccessor httpContext, ConnectorPackageMcpClient remoteMcp, ITenantCredentialProtector protector) : IConnectorPackageInstallationService
{
    public async Task<IReadOnlyList<ConnectorPackageInstallationItem>> ListAsync(CancellationToken cancellationToken = default)
    {
        await RequireUserAsync(["Integration.Read"], cancellationToken);
        var servers = await database.TenantMcpServers.AsNoTracking().Where(row => row.TenantId == TenantId && row.ConnectorPackageJson != null)
            .OrderBy(row => row.Name).Take(101).ToListAsync(cancellationToken);
        var tools = await database.TenantHttpTools.AsNoTracking().Where(row => row.TenantId == TenantId && row.ConnectorPackageJson != null)
            .OrderBy(row => row.Name).Take(101).ToListAsync(cancellationToken);
        if (servers.Count + tools.Count > 100) throw new InvalidOperationException("ConnectorPackage.InstallationListLimit");
        return servers.Select(Map).Concat(tools.Select(Map)).ToArray();
    }

    public async Task<IReadOnlyList<ConnectorPackageInstallationItem>> ImportAsync(ImportConnectorPackageRequest request, CancellationToken cancellationToken = default)
    {
        await RequireUserAsync(["Integration.Manage", "Workflow.Write"], cancellationToken);
        if (request.Digest is not { Length: 64 } || !request.Digest.All(Uri.IsHexDigit)) throw new InvalidOperationException("ConnectorPackage.PackageDigestChanged");
        VerifiedConnectorPackage package = catalog.Require(request.PackageKey, request.PackageVersion, request.Digest);
        if (package.Manifest.AdapterKind == "Native") throw new InvalidOperationException("Native adapters are installed by deployment. Use the deployed connector's ordinary account consent flow.");
        ValidateCredential(package, request.AuthSecret, request.AuthUsername);
        foreach (ConnectorPackageAction action in package.Manifest.Actions)
            if (!egress.IsAllowed(action.Endpoint, out _)) throw new PermissionDeniedException("Integration.Manage", "The signed endpoint is not allowed by current deployment egress policy.");
        if (package.Manifest.AdapterKind == "Mcp")
        {
            string name = ConnectorPackageReferences.ServerName(package);
            TenantMcpServer? existing = await database.TenantMcpServers.SingleOrDefaultAsync(row => row.TenantId == TenantId && row.Name == name, cancellationToken);
            if (existing is not null) { RequireConfiguration(existing); return [Map(existing)]; }
            var input = new SaveMcpServerRequest(name, package.Manifest.Actions[0].Endpoint, "Http", package.Manifest.Authentication.Kind,
                request.AuthSecret, request.AuthUsername, package.Manifest.Authentication.HeaderName,
                package.Manifest.Actions.Select(action => action.RemoteToolName!).ToArray());
            try { _ = await mcp.CreatePackageAsync(input, ConnectorPackageReferences.Serialize(package), cancellationToken); }
            catch (DbUpdateException error) when (error.InnerException is SqlException { Number: 2601 or 2627 })
            {
                foreach (var entry in database.ChangeTracker.Entries<TenantMcpServer>().Where(entry => entry.State == EntityState.Added && entry.Entity.TenantId == TenantId && entry.Entity.Name == name))
                    entry.State = EntityState.Detached;
            }
            TenantMcpServer importedServer = await database.TenantMcpServers.SingleAsync(row => row.TenantId == TenantId && row.Name == name, cancellationToken);
            RequireConfiguration(importedServer); return [Map(importedServer)];
        }
        var imported = new List<ConnectorPackageInstallationItem>();
        foreach (ConnectorPackageAction action in package.Manifest.Actions)
        {
            string name = ConnectorPackageReferences.HttpToolName(package, action);
            TenantHttpTool? existing = await database.TenantHttpTools.SingleOrDefaultAsync(row => row.TenantId == TenantId && row.Name == name, cancellationToken);
            if (existing is not null) { RequireConfiguration(existing); imported.Add(Map(existing)); continue; }
            var input = new SaveHttpToolRequest(name, action.DisplayName, action.HttpMethod!, action.Endpoint,
                CanonicalJson.Serialize(action.InputSchema), package.Manifest.Authentication.Kind, request.AuthSecret, request.AuthUsername, package.Manifest.Authentication.HeaderName);
            try { _ = await http.CreatePackageAsync(input, ConnectorPackageReferences.Serialize(package, new(action.Key, action.Version)), cancellationToken); }
            catch (DbUpdateException error) when (error.InnerException is SqlException { Number: 2601 or 2627 })
            {
                foreach (var entry in database.ChangeTracker.Entries<TenantHttpTool>().Where(entry => entry.State == EntityState.Added && entry.Entity.TenantId == TenantId && entry.Entity.Name == name))
                    entry.State = EntityState.Detached;
            }
            TenantHttpTool importedTool = await database.TenantHttpTools.SingleAsync(row => row.TenantId == TenantId && row.Name == name, cancellationToken);
            RequireConfiguration(importedTool); imported.Add(Map(importedTool));
        }
        return imported;
    }

    public async Task<ConnectorPackageInstallationItem> SubmitAsync(Guid id, ConnectorPackageInstallationCommand command, CancellationToken cancellationToken = default)
    {
        await RequireUserAsync(["Integration.Manage", "Workflow.Write"], cancellationToken);
        TenantMcpServer? server = await FindMcpAsync(id, cancellationToken);
        if (server is not null)
        {
            Version(server, command); RequireConfiguration(server); RequireCredentials(server.AuthKind, server.ProtectedAuthJson);
            _ = await mcp.SubmitPackageAsync(id, cancellationToken); return Map(server);
        }
        TenantHttpTool tool = await FindHttpAsync(id, cancellationToken);
        Version(tool, command); RequireConfiguration(tool); RequireCredentials(tool.AuthKind, tool.ProtectedAuthJson);
        _ = await http.SubmitPackageAsync(id, cancellationToken); return Map(tool);
    }

    public async Task<ConnectorPackageInstallationItem> DisableAsync(Guid id, ConnectorPackageInstallationCommand command, CancellationToken cancellationToken = default)
    {
        await RequireUserAsync(["Integration.Manage", "Workflow.Write"], cancellationToken);
        TenantMcpServer? server = await FindMcpAsync(id, cancellationToken);
        if (server is not null) { Version(server, command); _ = await mcp.DeactivateAsync(id, cancellationToken); return Map(server); }
        TenantHttpTool tool = await FindHttpAsync(id, cancellationToken);
        Version(tool, command); _ = await http.DeactivateAsync(id, cancellationToken); return Map(tool);
    }

    public async Task<ConnectorPackageInstallationItem> ReviewAsync(Guid id, ReviewConnectorPackageInstallationRequest request, CancellationToken cancellationToken = default)
    {
        await RequireUserAsync(["Integration.Manage", "Workflow.Write"], cancellationToken);
        if (httpContext.HttpContext?.User is not { } principal || !(await authorization.AuthorizeAsync(principal, null, "PlatformAdmin")).Succeeded)
            throw new PermissionDeniedException("PlatformAdmin");
        var command = new ConnectorPackageInstallationCommand(request.ExpectedVersion, request.Notes);
        ValidateNotes(request.Notes);
        TenantMcpServer? server = await FindMcpAsync(id, cancellationToken);
        if (server is not null)
        {
            Version(server, command); RequirePending(server.ApprovalState);
            if (request.Approve)
            {
                VerifiedConnectorPackage package = RequireConfiguration(server);
                RequireCredentials(server.AuthKind, server.ProtectedAuthJson);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(package.Manifest.Limits.TimeoutSeconds));
                try { await using var client = await remoteMcp.ConnectAsync(server, package, timeout.Token); }
                catch (Exception error) when (error is ConnectorProviderException || error is OperationCanceledException && !cancellationToken.IsCancellationRequested)
                { throw new InvalidOperationException("The reviewed MCP endpoint did not return its signed contracts. Check availability, credentials and egress policy before approval."); }
            }
            _ = await mcp.ReviewPackageAsync(id, new(request.Approve, request.Notes, "High"), cancellationToken);
            return Map(server);
        }
        TenantHttpTool tool = await FindHttpAsync(id, cancellationToken);
        Version(tool, command); RequirePending(tool.ApprovalState);
        string tier = "High";
        if (request.Approve)
        {
            var (package, action) = RequireConfiguration(tool);
            _ = TenantRemoteAuth.RequireHeaders(tool.AuthKind, tool.ProtectedAuthJson, protector, package.Manifest.Authentication.HeaderName);
            tier = action.MinimumRiskTier == "ReadOnly" ? "ReadOnly" : "High";
        }
        _ = await http.ReviewPackageAsync(id, new(request.Approve, request.Notes, tier), cancellationToken);
        return Map(tool);
    }

    public async Task<ConnectorPackageInstallationItem> ActivateAsync(Guid id, ConnectorPackageInstallationCommand command, CancellationToken cancellationToken = default)
    {
        await RequireUserAsync(["Integration.Manage", "Workflow.Write"], cancellationToken);
        TenantMcpServer? server = await FindMcpAsync(id, cancellationToken);
        if (server is not null)
        {
            Version(server, command); RequireConfiguration(server); RequireCredentials(server.AuthKind, server.ProtectedAuthJson);
            if (!egress.IsAllowed(server.Endpoint, out _)) throw Changed();
            _ = await mcp.ActivatePackageAsync(id, cancellationToken); return Map(server);
        }
        TenantHttpTool tool = await FindHttpAsync(id, cancellationToken);
        Version(tool, command); RequireConfiguration(tool); RequireCredentials(tool.AuthKind, tool.ProtectedAuthJson);
        if (!egress.IsAllowed(tool.UrlTemplate, out _)) throw Changed();
        _ = await http.ActivatePackageAsync(id, cancellationToken); return Map(tool);
    }

    public async Task<ConnectorPackageInstallationItem> RotateCredentialAsync(Guid id, RotateConnectorPackageCredentialRequest request, CancellationToken cancellationToken = default)
    {
        await RequireUserAsync(["Integration.Manage", "Workflow.Write"], cancellationToken);
        var command = new ConnectorPackageInstallationCommand(request.ExpectedVersion);
        if (string.IsNullOrWhiteSpace(request.AuthSecret)) throw new InvalidOperationException("Provide the new service credential.");
        TenantMcpServer? server = await FindMcpAsync(id, cancellationToken);
        if (server is not null)
        {
            Version(server, command);
            VerifiedConnectorPackage package = RequireConfiguration(server);
            ValidateCredential(package, request.AuthSecret, request.AuthUsername);
            await mcp.UpdateAsync(id, new(server.Name, server.Endpoint, server.TransportType.ToString(), server.AuthKind.ToString(),
                request.AuthSecret, request.AuthUsername, package.Manifest.Authentication.HeaderName,
                package.Manifest.Actions.Select(action => action.RemoteToolName!).ToArray()), cancellationToken);
            return Map(server);
        }
        TenantHttpTool tool = await FindHttpAsync(id, cancellationToken);
        Version(tool, command);
        var (release, _) = RequireConfiguration(tool);
        ValidateCredential(release, request.AuthSecret, request.AuthUsername);
        await http.UpdateAsync(id, new(tool.Name, tool.Description, tool.Method, tool.UrlTemplate, tool.ParameterSchemaJson, tool.AuthKind.ToString(),
            request.AuthSecret, request.AuthUsername, release.Manifest.Authentication.HeaderName), cancellationToken);
        return Map(tool);
    }

    internal VerifiedConnectorPackage RequireConfiguration(TenantMcpServer server)
        => ConnectorPackageReferences.Require(catalog, server, TenantId);

    internal (VerifiedConnectorPackage Package, ConnectorPackageAction Action) RequireConfiguration(TenantHttpTool tool)
        => ConnectorPackageReferences.Require(catalog, tool, TenantId);

    private Guid TenantId => tenantProvider.GetCurrentTenantId();
    private Task<TenantMcpServer?> FindMcpAsync(Guid id, CancellationToken cancellationToken) => database.TenantMcpServers.SingleOrDefaultAsync(row => row.TenantId == TenantId && row.Id == id && row.ConnectorPackageJson != null, cancellationToken);
    private async Task<TenantHttpTool> FindHttpAsync(Guid id, CancellationToken cancellationToken) => await database.TenantHttpTools.SingleOrDefaultAsync(row => row.TenantId == TenantId && row.Id == id && row.ConnectorPackageJson != null, cancellationToken) ?? throw Missing();
    private async Task<Guid> RequireUserAsync(string[] required, CancellationToken cancellationToken)
    {
        if (currentUser.GetCurrentUserId() is not { } id || id == Guid.Empty || !(await users.GetByIdsAsync(TenantId, [id], cancellationToken)).Any(user => user.UserId == id && user.Status == "Active"))
            throw new PermissionDeniedException(required[0]);
        foreach (string permission in required) if (!await permissions.HasPermissionAsync(id, permission, cancellationToken)) throw new PermissionDeniedException(permission);
        return id;
    }
    private static void Version(AuditableEntity entity, ConnectorPackageInstallationCommand command)
    {
        if (command.ExpectedVersion != Convert.ToBase64String(entity.RowVersion)) throw Changed();
    }
    private static void RequireCredentials(TenantToolAuthKind kind, string? credentials)
    {
        if (kind != TenantToolAuthKind.None && string.IsNullOrWhiteSpace(credentials)) throw new InvalidOperationException("Configure the service credential before requesting review.");
    }
    private static void RequirePending(TenantExtensionApprovalState state)
    {
        if (state != TenantExtensionApprovalState.PendingPlatformReview) throw new InvalidOperationException("Submit this installation before platform review.");
    }
    private static void ValidateNotes(string? notes)
    {
        if (notes is { Length: > 2000 }) throw new InvalidOperationException("Review notes must contain at most 2000 characters.");
    }
    private static void ValidateCredential(VerifiedConnectorPackage package, string? secret, string? username)
    {
        string kind = package.Manifest.Authentication.Kind;
        if (secret is { Length: > 16_384 } || username is { Length: > 500 } || secret?.Any(char.IsControl) == true
            || username?.Any(char.IsControl) == true || (kind == "None" && (secret is not null || username is not null))
            || (kind == "Basic" && secret is not null && (string.IsNullOrWhiteSpace(username) || username.Contains(':')))
            || (kind != "Basic" && username is not null))
            throw new InvalidOperationException("ConnectorPackage.CredentialInvalid");
    }
    private ConnectorPackageInstallationItem Map(TenantMcpServer row)
    {
        var reference = JsonSerializer.Deserialize<ConnectorPackageInstallationRef>(row.ConnectorPackageJson!, ConnectorPackageVerifier.JsonOptions)!;
        return new(row.Id, "Mcp", row.Name, reference.PackageKey, reference.PackageVersion, reference.Digest, null, row.ApprovalState.ToString(), row.IsActive,
            !string.IsNullOrWhiteSpace(row.ProtectedAuthJson), row.CredentialVersion, Convert.ToBase64String(row.RowVersion));
    }
    private ConnectorPackageInstallationItem Map(TenantHttpTool row)
    {
        var reference = JsonSerializer.Deserialize<ConnectorPackageInstallationRef>(row.ConnectorPackageJson!, ConnectorPackageVerifier.JsonOptions)!;
        return new(row.Id, "Http", row.Name, reference.PackageKey, reference.PackageVersion, reference.Digest, reference.Action, row.ApprovalState.ToString(), row.IsActive,
            !string.IsNullOrWhiteSpace(row.ProtectedAuthJson), row.CredentialVersion, Convert.ToBase64String(row.RowVersion));
    }
    private static InvalidOperationException Changed() => new("ConnectorPackage.InstallationChanged");
    private static KeyNotFoundException Missing() => new("Curated connector installation not found.");
}
