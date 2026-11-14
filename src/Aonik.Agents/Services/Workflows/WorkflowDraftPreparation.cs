using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Entities.Workflows;
using Aonik.Agents.Persistence;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Persistence;

namespace Aonik.Agents.Services.Workflows;

internal sealed record PreparedWorkflowDraft(WorkflowSaveRequest Draft, ToolApprovalPreview Preview);

/// <summary>Read-only preparation for a human to review the exact draft mutation before its gate runs.</summary>
internal sealed class WorkflowDraftPreparation(AgentsDbContext database, WorkflowAuthorization authorization)
{
    public async Task<PreparedWorkflowDraft> PrepareAsync(
        WorkflowSaveRequest request, bool creating, CancellationToken cancellationToken = default)
    {
        await authorization.RequireAsync("Workflow.Write", cancellationToken);
        WorkflowService.ValidateDraft(request);
        Workflow? workflow = await database.Workflows.AsNoTracking().SingleOrDefaultAsync(
            workflow => workflow.TenantId == authorization.TenantId && workflow.Slug == request.Slug, cancellationToken);
        if (creating)
        {
            if (await database.Workflows.IncludeSoftDeleted().AnyAsync(
                workflow => workflow.TenantId == authorization.TenantId && workflow.Slug == request.Slug, cancellationToken))
                throw new WorkflowConflictException("The workflow slug is already reserved.");
            if (!string.IsNullOrWhiteSpace(request.State) && !string.Equals(request.State, WorkflowStates.Draft, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("New workflows are drafts.");
        }
        else
        {
            if (workflow is null) throw new KeyNotFoundException("Workflow not found.");
            WorkflowService.CheckVersion(workflow, request.ExpectedVersion);
            if (request.DefinitionKind != workflow.DefinitionKind || (!string.IsNullOrWhiteSpace(request.State) && !string.Equals(request.State,
                WorkflowService.EffectiveState(workflow), StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("Draft editing cannot change definition kind or publication state.");
        }

        List<WorkflowNode> nodes = workflow is null ? [] : await database.WorkflowNodes.AsNoTracking()
            .Where(node => node.WorkflowId == workflow.Id).ToListAsync(cancellationToken);
        List<WorkflowEdge> edges = workflow is null ? [] : await database.WorkflowEdges.AsNoTracking()
            .Where(edge => edge.WorkflowId == workflow.Id).ToListAsync(cancellationToken);
        var selected = new HashSet<Guid>();
        var keys = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (WorkflowSaveNode requested in request.Nodes)
        {
            string requestedKey = requested.Key ?? requested.ClientId;
            List<WorkflowNode> matching = nodes.Where(node => Key(node) == requestedKey
                || (Guid.TryParse(requested.ClientId, out Guid id) && id == node.Id)).ToList();
            if (matching.Count > 1 || matching.Any(node => !selected.Add(node.Id)
                || (requested.Key is not null && requested.Key != Key(node))))
                throw new ArgumentException("Existing node keys cannot be renamed or aliased.");
            keys.Add(requested.ClientId, matching.Count == 1 ? Key(matching[0]) : requestedKey);
        }
        WorkflowSaveRequest canonical = request with
        {
            State = workflow is null ? WorkflowStates.Draft : WorkflowService.EffectiveState(workflow),
            Version = workflow is null ? "v0.1" : await WorkflowService.NextAvailableVersionAsync(database, workflow, cancellationToken),
            Nodes = request.Nodes.Select(node => node with { ClientId = keys[node.ClientId], Key = keys[node.ClientId] })
                .OrderBy(node => node.Key, StringComparer.Ordinal).ToList(),
            Edges = request.Edges.Select(edge => edge with { FromClientId = keys[edge.FromClientId], ToClientId = keys[edge.ToClientId] })
                .OrderBy(edge => edge.FromClientId, StringComparer.Ordinal).ThenBy(edge => edge.ToClientId, StringComparer.Ordinal)
                .ThenBy(edge => edge.FromIndex).ThenBy(edge => edge.RouteKey, StringComparer.Ordinal).ToList(),
        };
        JsonElement? before = workflow is null ? null : JsonSerializer.SerializeToElement(Current(workflow, nodes, edges), WorkflowDefinitionCodec.JsonOptions);
        return new(canonical, new($"{(creating ? "Create" : "Update")} workflow draft {request.Slug}", before,
            JsonSerializer.SerializeToElement(canonical, WorkflowDefinitionCodec.JsonOptions)));
    }

    public async Task<ToolApprovalPreview> PrepareDeletionAsync(string slug, string expectedVersion, CancellationToken cancellationToken = default)
    {
        await authorization.RequireAsync("Workflow.Write", cancellationToken);
        Workflow workflow = await database.Workflows.AsNoTracking().SingleOrDefaultAsync(
            workflow => workflow.TenantId == authorization.TenantId && workflow.Slug == slug, cancellationToken)
            ?? throw new KeyNotFoundException("Workflow not found.");
        WorkflowService.CheckVersion(workflow, expectedVersion);
        if (workflow.PublishedVersionId.HasValue || workflow.TriggerCount != 0
            || await database.WorkflowVersions.AnyAsync(version => version.WorkflowId == workflow.Id && version.PublishedAt != null, cancellationToken)
            || await database.WorkflowRuns.AnyAsync(run => run.WorkflowId == workflow.Id, cancellationToken))
            throw new WorkflowConflictException("Only an unpublished, unused workflow draft can be deleted.");
        List<WorkflowNode> nodes = await database.WorkflowNodes.AsNoTracking().Where(node => node.WorkflowId == workflow.Id).ToListAsync(cancellationToken);
        List<WorkflowEdge> edges = await database.WorkflowEdges.AsNoTracking().Where(edge => edge.WorkflowId == workflow.Id).ToListAsync(cancellationToken);
        return new($"Delete unpublished workflow draft {slug}", JsonSerializer.SerializeToElement(Current(workflow, nodes, edges), WorkflowDefinitionCodec.JsonOptions), null);
    }

    private static WorkflowSaveRequest Current(Workflow workflow, List<WorkflowNode> nodes, List<WorkflowEdge> edges)
    {
        Dictionary<Guid, string> keys = nodes.ToDictionary(node => node.Id, Key);
        return new(workflow.Slug, workflow.Name, workflow.Description, WorkflowService.EffectiveState(workflow), workflow.Version,
            workflow.AutoRetry, workflow.OwnerColor, workflow.OwnerAgentId, WorkflowService.ParseGuidArray(workflow.ContributorsJson),
            nodes.OrderBy(Key, StringComparer.Ordinal).Select(node => new WorkflowSaveNode(Key(node), node.Kind, node.Label, node.Summary, node.Notes, node.X, node.Y, node.ParamsJson, Key(node))).ToList(),
            edges.Select(edge => new WorkflowSaveEdge(keys[edge.FromNodeId], keys[edge.ToNodeId], edge.FromIndex, edge.Label, edge.RouteKey))
                .OrderBy(edge => edge.FromClientId, StringComparer.Ordinal).ThenBy(edge => edge.ToClientId, StringComparer.Ordinal)
                .ThenBy(edge => edge.FromIndex).ThenBy(edge => edge.RouteKey, StringComparer.Ordinal).ToList(),
            null, Convert.ToBase64String(workflow.RowVersion), workflow.DefinitionKind);
    }

    private static string Key(WorkflowNode node) => node.LogicalKey ?? node.Id.ToString("D");
}
