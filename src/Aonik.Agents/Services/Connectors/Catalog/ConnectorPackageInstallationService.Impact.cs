using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Aonik.Agents.Contracts.Models.Tenant;
using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Entities.Workflows;
using Aonik.Agents.Services.Workflows;
using Aonik.Agents.Workflows.Graph;
using Aonik.SharedKernel.Abstractions.Connectors;

namespace Aonik.Agents.Services.Connectors.Catalog;

internal sealed partial class ConnectorPackageInstallationService
{
    public async Task<ConnectorPackageImpact> PreviewAsync(Guid id, string? targetVersion = null, CancellationToken cancellationToken = default)
    {
        await RequireUserAsync(["Integration.Manage", "Workflow.Read"], cancellationToken);
        var server = await FindMcpAsync(id, cancellationToken);
        ConnectorPackageInstallationItem installation = server is not null ? Map(server) : Map(await FindHttpAsync(id, cancellationToken));
        VerifiedConnectorPackage? current = catalog.Packages.SingleOrDefault(package => package.Manifest.PackageKey == installation.PackageKey
            && package.Manifest.PackageVersion == installation.PackageVersion && package.Digest == installation.Digest);
        VerifiedConnectorPackage? target = string.IsNullOrEmpty(targetVersion) ? null : catalog.Require(installation.PackageKey, targetVersion);
        if (target is not null && target.Manifest.AdapterKind != installation.AdapterKind)
            throw new InvalidOperationException("Select a release with the same adapter kind.");

        const int limit = 100;
        var versions = await database.WorkflowVersions.AsNoTracking()
            .Where(version => version.TenantId == TenantId && version.DefinitionJson != null && version.DefinitionJson.Contains(installation.Digest))
            .OrderByDescending(version => version.PublishedAt).ThenBy(version => version.Id).Take(limit + 1).ToListAsync(cancellationToken);
        WorkflowVersion[] directlyBound = versions.Take(limit).Where(UsesInstallation).ToArray();
        Guid[] workflowIds = directlyBound.Select(version => version.WorkflowId).Distinct().ToArray();
        var workflows = await database.Workflows.AsNoTracking().Where(workflow => workflow.TenantId == TenantId && workflowIds.Contains(workflow.Id))
            .ToDictionaryAsync(workflow => workflow.Id, cancellationToken);
        ConnectorPackageWorkflowUsage[] usages = directlyBound.Where(version => workflows.ContainsKey(version.WorkflowId)).Select(version =>
        {
            Workflow workflow = workflows[version.WorkflowId];
            return new ConnectorPackageWorkflowUsage(workflow.Id, version.Id, workflow.Slug, version.Tag, workflow.PublishedVersionId == version.Id);
        }).ToArray();
        Guid[] versionIds = directlyBound.Select(version => version.Id).ToArray();
        var runs = await database.WorkflowRuns.AsNoTracking().Where(run => run.TenantId == TenantId && run.WorkflowVersionId.HasValue
                && versionIds.Contains(run.WorkflowVersionId.Value) && run.Mode == "Live" && run.Status != WorkflowRunStatuses.Completed
                && run.Status != WorkflowRunStatuses.Cancelled && run.Status != WorkflowRunStatuses.Failed)
            .OrderBy(run => run.StartedAt).ThenBy(run => run.Id).Take(limit + 1)
            .Select(run => new ConnectorPackageRunUsage(run.Id, run.WorkflowVersionId!.Value, run.Status)).ToListAsync(cancellationToken);
        return new(installation, target is null ? null : ConnectorPackageCatalogService.Summary(target),
            Actions(current, installation.Action), Actions(target, null), usages, runs.Take(limit).ToArray(), versions.Count > limit || runs.Count > limit);

        bool UsesInstallation(WorkflowVersion version) => WorkflowRunService.ReadDefinition(version).Nodes
            .Where(node => WorkflowDefinitionValidator.IsKind(node, WorkflowNodeKinds.Tool))
            .Select(node => node.Parameters.Deserialize<WorkflowToolParameters>(WorkflowDefinitionCodec.JsonOptions))
            .Any(parameters => parameters?.Connection?.Artifact is { } pin && pin.PackageKey == installation.PackageKey
                && pin.PackageVersion == installation.PackageVersion && pin.ArtifactDigest == installation.Digest
                && (installation.Action is null || installation.Action == new ConnectorActionRef(parameters.ActionKey, parameters.ContractVersion)));
    }

    private static IReadOnlyList<ConnectorPackageReviewedAction> Actions(VerifiedConnectorPackage? package, ConnectorActionRef? only)
        => package?.Manifest.Actions.Where(action => only is null || only == new ConnectorActionRef(action.Key, action.Version))
            .Select(action => new ConnectorPackageReviewedAction(new(action.Key, action.Version), action.MinimumRiskTier == "ReadOnly" ? "ReadOnly" : "High",
                action.RequiredScopes, package.Pin(action).ActionContractHash)).ToArray() ?? [];
}
