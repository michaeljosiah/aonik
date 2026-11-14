using System.Text.Json;
using System.Text.RegularExpressions;

using Microsoft.EntityFrameworkCore;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Contracts.Services;
using Aonik.Agents.Entities;
using Aonik.Agents.Entities.Workflows;
using Aonik.Agents.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Persistence;

namespace Aonik.Agents.Services.Workflows;

/// <summary>
/// Tenant-authorized workflow drafts, immutable history and registry summaries.
/// </summary>
internal sealed class WorkflowService : IWorkflowService
{
    private readonly AgentsDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IClock _clock;
    private readonly WorkflowAuthorization _authorization;

    public WorkflowService(
        AgentsDbContext dbContext,
        ITenantProvider tenantProvider,
        IClock clock,
        WorkflowAuthorization authorization)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _clock = clock;
        _authorization = authorization;
    }

    public async Task<IReadOnlyList<WorkflowSummaryResponse>> ListAsync(
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        await _authorization.RequireAsync("Workflow.Read", cancellationToken);
        if (skip is < 0 or > 100_000 || take is < 1 or > 100)
            throw new ArgumentException("Use a nonnegative offset up to 100000 and between 1 and 100 workflows.");
        var workflows = await _dbContext.Workflows
            .Where(w => w.TenantId == _tenantProvider.GetCurrentTenantId())
            .AsNoTracking()
            .OrderByDescending(w => w.UpdatedAt ?? w.CreatedAt)
            .ThenBy(w => w.Id)
            .Skip(skip).Take(take)
            .ToListAsync(cancellationToken);

        if (workflows.Count == 0)
        {
            return Array.Empty<WorkflowSummaryResponse>();
        }

        var workflowIds = workflows.Select(w => w.Id).ToList();
        var ownerAgentIds = workflows
            .Where(w => w.OwnerAgentId.HasValue)
            .Select(w => w.OwnerAgentId!.Value)
            .Distinct()
            .ToList();

        // Pull all the joinable rows in three short round-trips rather than
        // N+1 per workflow.
        var nodes = await _dbContext.WorkflowNodes
            .AsNoTracking()
            .Where(n => workflowIds.Contains(n.WorkflowId))
            .OrderBy(n => n.Y)
            .ThenBy(n => n.X)
            .ToListAsync(cancellationToken);

        var since = _clock.UtcNow.AddHours(-24);
        var recentRuns = await _dbContext.WorkflowRuns
            .AsNoTracking()
            .Where(r => workflowIds.Contains(r.WorkflowId) && r.StartedAt >= since)
            .ToListAsync(cancellationToken);

        var owners = await _dbContext.Agents
            .AsNoTracking()
            .Where(a => ownerAgentIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, cancellationToken);

        var nodesByWorkflow = nodes
            .GroupBy(n => n.WorkflowId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var runsByWorkflow = recentRuns
            .GroupBy(r => r.WorkflowId)
            .ToDictionary(g => g.Key, g => g.ToList());

        return workflows.Select(w =>
        {
            var ownerName = w.OwnerAgentId.HasValue && owners.TryGetValue(w.OwnerAgentId.Value, out var owner)
                ? owner.Name
                : "Unassigned";

            var contributorIds = ParseGuidArray(w.ContributorsJson);
            var contributorNames = contributorIds
                .Select(id => owners.TryGetValue(id, out var c) ? c.Name : null)
                .Where(name => !string.IsNullOrEmpty(name))
                .Cast<string>()
                .ToList();

            var workflowNodes = nodesByWorkflow.GetValueOrDefault(w.Id, new List<WorkflowNode>());
            var workflowRuns = runsByWorkflow.GetValueOrDefault(w.Id, new List<WorkflowRun>());

            return new WorkflowSummaryResponse(
                Id: w.Id,
                Slug: w.Slug,
                Name: w.Name,
                Description: w.Description,
                State: EffectiveState(w),
                Version: w.Version,
                AutoRetry: w.AutoRetry,
                TriggerCount: w.TriggerCount,
                RunsToday: workflowRuns.Count,
                Success: ComputeSuccessRate(workflowRuns),
                AvgMs: ComputeAvgMs(workflowRuns),
                OwnerName: ownerName,
                OwnerColor: w.OwnerColor,
                Contributors: contributorNames,
                Steps: workflowNodes
                    .OrderBy(n => n.X)
                    .Select(n => new WorkflowStepSummary(n.Kind, n.Label, n.Summary))
                    .ToList(),
                UpdatedAt: w.UpdatedAt ?? w.CreatedAt);
        }).ToList();
    }

    public async Task<WorkflowGraphResponse?> GetBySlugAsync(
        string slug,
        CancellationToken cancellationToken = default)
    {
        await _authorization.RequireAsync("Workflow.Read", cancellationToken);
        return await GetGraphAsync(slug, cancellationToken);
    }

    private async Task<WorkflowGraphResponse?> GetGraphAsync(string slug, CancellationToken cancellationToken)
    {
        var workflow = await _dbContext.Workflows
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.TenantId == _tenantProvider.GetCurrentTenantId() && w.Slug == slug, cancellationToken);

        if (workflow is null) return null;

        var nodes = await _dbContext.WorkflowNodes
            .AsNoTracking()
            .Where(n => n.WorkflowId == workflow.Id)
            .ToListAsync(cancellationToken);

        var edges = await _dbContext.WorkflowEdges
            .AsNoTracking()
            .Where(e => e.WorkflowId == workflow.Id)
            .ToListAsync(cancellationToken);

        var comments = await _dbContext.WorkflowComments
            .AsNoTracking()
            .Where(c => c.WorkflowId == workflow.Id)
            .ToListAsync(cancellationToken);

        var ownerName = "Unassigned";
        var contributorNames = new List<string>();
        var contributorIds = ParseGuidArray(workflow.ContributorsJson);

        var agentIds = contributorIds.ToHashSet();
        if (workflow.OwnerAgentId.HasValue) agentIds.Add(workflow.OwnerAgentId.Value);

        if (agentIds.Count > 0)
        {
            var agentDict = await _dbContext.Agents
                .AsNoTracking()
                .Where(a => agentIds.Contains(a.Id))
                .ToDictionaryAsync(a => a.Id, cancellationToken);

            if (workflow.OwnerAgentId.HasValue && agentDict.TryGetValue(workflow.OwnerAgentId.Value, out var owner))
            {
                ownerName = owner.Name;
            }

            contributorNames = contributorIds
                .Select(id => agentDict.TryGetValue(id, out var c) ? c.Name : null)
                .Where(name => !string.IsNullOrEmpty(name))
                .Cast<string>()
                .ToList();
        }

        Dictionary<Guid, string> nodeKeys = nodes.ToDictionary(node => node.Id, node => node.LogicalKey ?? node.Id.ToString("D"));
        return new WorkflowGraphResponse(
            Id: workflow.Id,
            Slug: workflow.Slug,
            Name: workflow.Name,
            Description: workflow.Description,
            State: EffectiveState(workflow),
            Version: workflow.Version,
            AutoRetry: workflow.AutoRetry,
            OwnerColor: workflow.OwnerColor,
            OwnerName: ownerName,
            Contributors: contributorNames,
            Nodes: nodes.Select(n => new WorkflowGraphNode(
                n.Id, n.Kind, n.Label, n.Summary, n.Notes, n.X, n.Y, n.ParamsJson, n.LogicalKey ?? n.Id.ToString("D"))).ToList(),
            Edges: edges.Select(e => new WorkflowGraphEdge(
                e.Id, e.FromNodeId, e.ToNodeId, e.FromIndex, e.Label, e.RouteKey,
                WorkflowDefinitionCodec.EdgeKey(nodeKeys[e.FromNodeId], nodeKeys[e.ToNodeId], e.FromIndex, e.RouteKey))).ToList(),
            Comments: comments.Select(c => new WorkflowGraphComment(
                c.Id, c.X, c.Y, c.Author, c.Body)).ToList(),
            ExpectedVersion: Convert.ToBase64String(workflow.RowVersion),
            DefinitionKind: workflow.DefinitionKind,
            PublishedVersionId: workflow.PublishedVersionId);
    }

    public async Task<IReadOnlyList<WorkflowRunResponse>> ListRunsAsync(
        Guid workflowId,
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        await _authorization.RequireAsync("Workflow.Read", cancellationToken);
        var runs = await _dbContext.WorkflowRuns
            .Where(r => r.TenantId == _tenantProvider.GetCurrentTenantId())
            .AsNoTracking()
            .Where(r => r.WorkflowId == workflowId)
            .OrderByDescending(r => r.StartedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

        var now = _clock.UtcNow;

        return runs.Select(r =>
        {
            var sequence = ParseGuidArray(r.SequenceJson);
            return new WorkflowRunResponse(
                Id: r.Id,
                StartedAt: r.StartedAt,
                CompletedAt: r.CompletedAt,
                When: FormatRelative(now, r.StartedAt),
                Status: r.Status,
                Duration: FormatDuration(r.DurationMs),
                DurationMs: r.DurationMs,
                By: r.StartedBy,
                Sequence: sequence,
                Total: sequence.Count);
        }).ToList();
    }

    public async Task<WorkflowGraphResponse> CreateAsync(
        WorkflowSaveRequest request, CancellationToken cancellationToken = default)
    {
        Guid userId = await _authorization.RequireAsync("Workflow.Write", cancellationToken);
        ValidateDraft(request);
        if (!string.IsNullOrEmpty(request.State) && !string.Equals(request.State, WorkflowStates.Draft, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("New workflows are drafts. Use a publication request to activate one.");
        if (await _dbContext.Workflows.IncludeSoftDeleted().AnyAsync(
            workflow => workflow.TenantId == _tenantProvider.GetCurrentTenantId() && workflow.Slug == request.Slug, cancellationToken))
            throw new WorkflowConflictException("This workflow slug is already reserved in the tenant.");

        var workflow = new Workflow
        {
            TenantId = _tenantProvider.GetCurrentTenantId(),
            Slug = request.Slug,
            State = WorkflowStates.Draft,
            Version = "v0.1",
        };
        ApplyDraft(workflow, request);
        _dbContext.Workflows.Add(workflow);
        await ReplaceGraphAsync(workflow, request, userId, cancellationToken);
        return (await GetGraphAsync(workflow.Slug, cancellationToken))!;
    }

    public async Task<WorkflowGraphResponse?> UpdateAsync(
        WorkflowSaveRequest request, CancellationToken cancellationToken = default)
    {
        Guid userId = await _authorization.RequireAsync("Workflow.Write", cancellationToken);
        ValidateDraft(request);
        Workflow? workflow = await _dbContext.Workflows.SingleOrDefaultAsync(
            workflow => workflow.TenantId == _tenantProvider.GetCurrentTenantId() && workflow.Slug == request.Slug, cancellationToken);
        if (workflow is null) return null;
        CheckVersion(workflow, request.ExpectedVersion);
        if (!string.IsNullOrEmpty(request.State) && !string.Equals(request.State, EffectiveState(workflow), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("State changes require publication or retirement; editing changes only the draft.");
        if (request.DefinitionKind != workflow.DefinitionKind)
            throw new ArgumentException("The definition kind cannot change after creation.");
        await ValidateExistingKeysAsync(workflow.Id, request, cancellationToken);
        workflow.Version = await NextAvailableVersionAsync(_dbContext, workflow, cancellationToken);
        ApplyDraft(workflow, request);
        await ReplaceGraphAsync(workflow, request, userId, cancellationToken);
        return (await GetGraphAsync(workflow.Slug, cancellationToken))!;
    }

    public async Task<bool> DeleteAsync(
        string slug, string? expectedVersion, CancellationToken cancellationToken = default)
    {
        await _authorization.RequireAsync("Workflow.Write", cancellationToken);
        Workflow? workflow = await _dbContext.Workflows.SingleOrDefaultAsync(
            workflow => workflow.TenantId == _tenantProvider.GetCurrentTenantId() && workflow.Slug == slug, cancellationToken);
        if (workflow is null) return false;
        CheckVersion(workflow, expectedVersion);
        if (workflow.PublishedVersionId.HasValue || workflow.TriggerCount != 0
            || await _dbContext.WorkflowVersions.AnyAsync(version => version.WorkflowId == workflow.Id && version.PublishedAt != null, cancellationToken)
            || await _dbContext.WorkflowRuns.AnyAsync(run => run.WorkflowId == workflow.Id, cancellationToken))
            throw new WorkflowConflictException("A published, referenced or previously executed workflow must be retired, not deleted.");
        workflow.IsDeleted = true;
        workflow.DeletedAt = _clock.UtcNow;
        foreach (var node in await _dbContext.WorkflowNodes.Where(node => node.WorkflowId == workflow.Id).ToListAsync(cancellationToken))
        {
            node.IsDeleted = true;
            node.DeletedAt = _clock.UtcNow;
        }
        foreach (var edge in await _dbContext.WorkflowEdges.Where(edge => edge.WorkflowId == workflow.Id).ToListAsync(cancellationToken))
        {
            edge.IsDeleted = true;
            edge.DeletedAt = _clock.UtcNow;
        }
        foreach (var comment in await _dbContext.WorkflowComments.Where(comment => comment.WorkflowId == workflow.Id).ToListAsync(cancellationToken))
        {
            comment.IsDeleted = true;
            comment.DeletedAt = _clock.UtcNow;
        }
        await SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<WorkflowValidation?> ValidateAsync(string slug, CancellationToken cancellationToken = default)
    {
        await _authorization.RequireAsync("Workflow.Read", cancellationToken);
        WorkflowGraphResponse? graph = await GetGraphAsync(slug, cancellationToken);
        if (graph is null) return null;
        WorkflowDefinition definition = WorkflowDefinitionCodec.FromGraph(graph);
        string hash = WorkflowDefinitionCodec.Hash(WorkflowDefinitionCodec.Serialize(definition));
        IReadOnlyList<WorkflowDiagnostic> diagnostics = WorkflowDefinitionValidator.Validate(definition, forPublication: true);
        return new(!diagnostics.Any(diagnostic => diagnostic.Severity == "Error"), hash, diagnostics);
    }

    public async Task<WorkflowSimulation?> SimulateAsync(
        string slug, WorkflowSimulationRequest request, CancellationToken cancellationToken = default)
    {
        await _authorization.RequireAsync("Workflow.Read", cancellationToken);
        if (request.Facts is null || request.Facts.Count > 100 || request.ActionOutputs?.Count > WorkflowDefinitionValidator.MaxNodes
            || JsonSerializer.Serialize(request).Length > 128_000)
            throw new ArgumentException("Fixture input exceeds the supported limits.");
        WorkflowGraphResponse? graph = await GetGraphAsync(slug, cancellationToken);
        return graph is null ? null : WorkflowSimulator.Simulate(WorkflowDefinitionCodec.FromGraph(graph), request);
    }

    public async Task<WorkflowCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default)
    {
        await _authorization.RequireAsync("Workflow.Read", cancellationToken);
        return WorkflowDefinitionValidator.Capabilities();
    }

    private static void ApplyDraft(Workflow workflow, WorkflowSaveRequest request)
    {
        if (!workflow.PublishedVersionId.HasValue) workflow.State = WorkflowStates.Draft;
        workflow.Name = request.Name;
        workflow.Description = request.Description ?? "";
        workflow.DefinitionKind = request.DefinitionKind;
        workflow.AutoRetry = request.AutoRetry;
        workflow.OwnerColor = request.OwnerColor ?? "";
        workflow.OwnerAgentId = request.OwnerAgentId;
        workflow.ContributorsJson = JsonSerializer.Serialize(request.Contributors);
    }

    internal static string EffectiveState(Workflow workflow) => workflow.PublishedVersionId.HasValue ? workflow.State : WorkflowStates.Draft;

    private async Task ReplaceGraphAsync(
        Workflow workflow, WorkflowSaveRequest request, Guid userId, CancellationToken cancellationToken)
    {
        List<WorkflowNode> existingNodes = await _dbContext.WorkflowNodes
            .Where(node => node.WorkflowId == workflow.Id).ToListAsync(cancellationToken);
        List<WorkflowEdge> existingEdges = await _dbContext.WorkflowEdges
            .Where(edge => edge.WorkflowId == workflow.Id).ToListAsync(cancellationToken);
        var clientNodes = new Dictionary<string, WorkflowNode>(StringComparer.Ordinal);
        foreach (WorkflowSaveNode requested in request.Nodes)
        {
            string key = requested.Key ?? requested.ClientId;
            WorkflowNode? node = existingNodes.SingleOrDefault(node => (node.LogicalKey ?? node.Id.ToString("D")) == key
                || IsNodeId(node.Id, requested.ClientId));
            if (node is null)
            {
                node = new WorkflowNode { TenantId = workflow.TenantId, WorkflowId = workflow.Id, LogicalKey = key };
                _dbContext.WorkflowNodes.Add(node);
            }
            else node.LogicalKey ??= node.Id.ToString("D");
            node.Kind = requested.Kind;
            node.Label = requested.Label;
            node.Summary = requested.Summary ?? "";
            node.Notes = requested.Notes ?? "";
            node.X = requested.X;
            node.Y = requested.Y;
            node.ParamsJson = string.IsNullOrWhiteSpace(requested.ParamsJson) ? "{}" : requested.ParamsJson;
            clientNodes.Add(requested.ClientId, node);
        }
        HashSet<Guid> retained = clientNodes.Values.Select(node => node.Id).ToHashSet();
        _dbContext.WorkflowNodes.RemoveRange(existingNodes.Where(node => !retained.Contains(node.Id)));
        var newEdges = new List<WorkflowEdge>();
        foreach (WorkflowSaveEdge requested in request.Edges)
        {
            Guid from = clientNodes[requested.FromClientId].Id;
            Guid to = clientNodes[requested.ToClientId].Id;
            WorkflowEdge? edge = existingEdges.FirstOrDefault(edge => edge.FromNodeId == from && edge.ToNodeId == to
                && edge.FromIndex == requested.FromIndex && edge.RouteKey == requested.RouteKey);
            if (edge is null)
            {
                edge = new WorkflowEdge { TenantId = workflow.TenantId, WorkflowId = workflow.Id };
                _dbContext.WorkflowEdges.Add(edge);
            }
            edge.FromNodeId = from;
            edge.ToNodeId = to;
            edge.FromIndex = requested.FromIndex;
            edge.RouteKey = requested.RouteKey;
            edge.Label = requested.Label ?? "";
            newEdges.Add(edge);
        }
        HashSet<Guid> retainedEdges = newEdges.Select(edge => edge.Id).ToHashSet();
        _dbContext.WorkflowEdges.RemoveRange(existingEdges.Where(edge => !retainedEdges.Contains(edge.Id)));
        AddSnapshot(workflow, clientNodes.Values.ToList(), newEdges, userId, request.VersionMessage);
        await SaveChangesAsync(cancellationToken);
    }

    private async Task ValidateExistingKeysAsync(Guid workflowId, WorkflowSaveRequest request, CancellationToken cancellationToken)
    {
        List<WorkflowNode> nodes = await _dbContext.WorkflowNodes.AsNoTracking()
            .Where(node => node.WorkflowId == workflowId).ToListAsync(cancellationToken);
        var selected = new HashSet<Guid>();
        foreach (WorkflowSaveNode requested in request.Nodes)
        {
            string key = requested.Key ?? requested.ClientId;
            var matches = nodes.Where(node => (node.LogicalKey ?? node.Id.ToString("D")) == key
                || IsNodeId(node.Id, requested.ClientId)).ToList();
            if (matches.Count > 1 || matches.Any(node => !selected.Add(node.Id)
                || (requested.Key is not null && requested.Key != (node.LogicalKey ?? node.Id.ToString("D")))))
                throw new ArgumentException("Existing node keys cannot be renamed or aliased by another node.");
        }
    }

    private void AddSnapshot(Workflow workflow, List<WorkflowNode> nodes, List<WorkflowEdge> edges, Guid userId, string? message)
    {
        Dictionary<Guid, string> keys = nodes.ToDictionary(node => node.Id, node => node.LogicalKey!);
        var definition = new WorkflowDefinition(1, workflow.DefinitionKind, workflow.Slug, workflow.Name, workflow.Description,
            nodes.OrderBy(node => node.LogicalKey, StringComparer.Ordinal).Select(node => new WorkflowDefinitionNode(
                node.LogicalKey!, node.Kind, node.Label, JsonSerializer.Deserialize<JsonElement>(node.ParamsJson))).ToList(),
            edges.Select(edge => new WorkflowDefinitionEdge(WorkflowDefinitionCodec.EdgeKey(keys[edge.FromNodeId], keys[edge.ToNodeId], edge.FromIndex, edge.RouteKey),
                keys[edge.FromNodeId], keys[edge.ToNodeId], edge.FromIndex, edge.RouteKey)).OrderBy(edge => edge.Key, StringComparer.Ordinal).ToList());
        string json = WorkflowDefinitionCodec.Serialize(definition);
        _dbContext.WorkflowVersions.Add(new WorkflowVersion
        {
            TenantId = workflow.TenantId, WorkflowId = workflow.Id, Tag = workflow.Version,
            Message = string.IsNullOrWhiteSpace(message) ? "Draft saved." : message,
            AuthorName = userId.ToString("D"), AuthorColor = workflow.OwnerColor, CreatedBy = userId,
            DefinitionJson = json, SchemaVersion = definition.SchemaVersion, DefinitionHash = WorkflowDefinitionCodec.Hash(json),
        });
    }

    private static bool IsNodeId(Guid nodeId, string clientId) => Guid.TryParse(clientId, out Guid id) && id == nodeId;

    internal static void CheckVersion(Workflow workflow, string? expectedVersion)
    {
        if (expectedVersion is null) throw new ArgumentException("ExpectedVersion is required for updates and deletion.");
        if (!string.Equals(expectedVersion, Convert.ToBase64String(workflow.RowVersion), StringComparison.Ordinal))
            throw new WorkflowConflictException("The workflow changed. Reload it before editing.");
    }

    private async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try { await _dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw new WorkflowConflictException("The workflow changed. Reload it before editing."); }
        catch (DbUpdateException ex) when (ex.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 })
        { throw new WorkflowConflictException("The workflow slug or version already exists."); }
    }

    internal static void ValidateDraft(WorkflowSaveRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Slug) || request.Slug.Length > 100
            || !Regex.IsMatch(request.Slug, "^[a-z0-9][a-z0-9_-]*$", RegexOptions.CultureInvariant))
            throw new ArgumentException("Provide a URL-safe tenant workflow slug of at most 100 characters.");
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 200 || request.Description?.Length > 2000
            || request.OwnerColor?.Length > 16 || request.VersionMessage?.Length > 1000)
            throw new ArgumentException("Workflow metadata exceeds the supported limits.");
        if (request.DefinitionKind is not ("Workflow" or "DecisionLibrary"))
            throw new ArgumentException("Unknown workflow definition kind.");
        if (request.Nodes is null || request.Edges is null || request.Contributors is null
            || request.Nodes.Count > WorkflowDefinitionValidator.MaxNodes || request.Edges.Count > WorkflowDefinitionValidator.MaxEdges
            || request.Contributors.Count > 64)
            throw new ArgumentException("Workflow graph exceeds the supported limits.");
        if (JsonSerializer.Serialize(request).Length > WorkflowDefinitionValidator.MaxDefinitionCharacters)
            throw new ArgumentException("Workflow definition exceeds the supported size.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (WorkflowSaveNode node in request.Nodes)
        {
            if (node is null) throw new ArgumentException("Nodes cannot be null.");
            string key = node.Key ?? node.ClientId;
            if (string.IsNullOrEmpty(node.ClientId) || node.ClientId.Length > 128 || !ids.Add(node.ClientId)
                || string.IsNullOrEmpty(key) || !Regex.IsMatch(key, "^[a-zA-Z0-9][a-zA-Z0-9_.-]{0,127}$", RegexOptions.CultureInvariant) || !keys.Add(key)
                || string.IsNullOrWhiteSpace(node.Kind) || node.Kind.Length > 32 || node.Label is null || node.Label.Length > 200
                || node.Summary?.Length > 500 || node.Notes?.Length > 2000 || node.ParamsJson?.Length > 64000)
                throw new ArgumentException("Node keys must be unique; node data must respect the declared limits.");
            if (node.X is < -100_000 or > 100_000 || node.Y is < -100_000 or > 100_000)
                throw new ArgumentException("Node positions exceed the supported canvas bounds.");
            try
            {
                using JsonDocument parameters = JsonDocument.Parse(string.IsNullOrWhiteSpace(node.ParamsJson) ? "{}" : node.ParamsJson, new JsonDocumentOptions { MaxDepth = 32 });
                if (parameters.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException();
                ValidateObject(parameters.RootElement);
            }
            catch (JsonException) { throw new ArgumentException("Node parameters must be a bounded JSON object without duplicate properties."); }
        }
        var edges = new HashSet<string>(StringComparer.Ordinal);
        foreach (WorkflowSaveEdge edge in request.Edges)
        {
            if (edge is null) throw new ArgumentException("Edges cannot be null.");
            if (!ids.Contains(edge.FromClientId) || !ids.Contains(edge.ToClientId) || edge.FromIndex < 0 || edge.Label?.Length > 64
                || (edge.RouteKey is not null && !WorkflowDefinitionCodec.IsKey(edge.RouteKey))
                || !edges.Add(WorkflowDefinitionCodec.EdgeKey(edge.FromClientId, edge.ToClientId, edge.FromIndex, edge.RouteKey)))
                throw new ArgumentException("Edges must have unique valid endpoints and route keys.");
        }

        static void ValidateObject(JsonElement value)
        {
            if (value.ValueKind == JsonValueKind.Object)
            {
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (JsonProperty property in value.EnumerateObject())
                {
                    if (!names.Add(property.Name)) throw new JsonException();
                    ValidateObject(property.Value);
                }
            }
            else if (value.ValueKind == JsonValueKind.Array)
                foreach (JsonElement child in value.EnumerateArray()) ValidateObject(child);
        }
    }

    public async Task<IReadOnlyList<WorkflowVersionResponse>> ListVersionsAsync(
        Guid workflowId,
        CancellationToken cancellationToken = default)
    {
        await _authorization.RequireAsync("Workflow.Read", cancellationToken);
        var versions = await _dbContext.WorkflowVersions
            .Where(v => v.TenantId == _tenantProvider.GetCurrentTenantId())
            .AsNoTracking()
            .Where(v => v.WorkflowId == workflowId)
            .ToListAsync(cancellationToken);

        var now = _clock.UtcNow;

        // Order by tag descending — version tags are monotonically
        // increasing semver-ish strings ("v1.4" > "v1.3" > "v1.1") so a
        // natural-sort comparison gives newest-first reliably. We avoid
        // ordering by CreatedAt because the audit hook stamps every
        // seeded row with the seed's "now", erasing the historical
        // timestamps the seed catalog supplies.
        var ordered = versions
            .OrderByDescending(v => v.Tag, NaturalVersionComparer.Instance)
            .ToList();

        return ordered.Select(v => new WorkflowVersionResponse(
            Id: v.Id,
            Tag: v.Tag,
            Message: v.Message,
            AuthorName: v.AuthorName,
            AuthorColor: v.AuthorColor,
            CreatedAt: v.CreatedAt,
            When: FormatRelative(now, v.CreatedAt),
            DefinitionHash: v.DefinitionHash,
            IsExecutable: v.PublishedAt.HasValue && v.DefinitionJson is not null,
            PublishedAt: v.PublishedAt,
            SchemaVersion: v.SchemaVersion)).ToList();
    }

    // ── Helpers ──────────────────────────────────────────────────

    /// <summary>
    /// Picks the next "vMAJOR.MINOR" version tag that doesn't yet have
    /// a <see cref="WorkflowVersion"/> row for this workflow. Walks from
    /// the existing tag forward — most saves only bump by one, but the
    /// seed can pre-populate intermediate tags so we keep stepping past
    /// any collisions instead of failing the save.
    /// </summary>
    internal static async Task<string> NextAvailableVersionAsync(
        AgentsDbContext database, Workflow workflow,
        CancellationToken cancellationToken)
    {
        var existingTags = await database.WorkflowVersions
            .AsNoTracking()
            .Where(v => v.WorkflowId == workflow.Id)
            .Select(v => v.Tag)
            .ToListAsync(cancellationToken);
        var taken = new HashSet<string>(existingTags, StringComparer.OrdinalIgnoreCase);

        var candidate = BumpMinorVersion(workflow.Version);
        // Defensive bound — workflows with hundreds of versions are
        // pathological, but we'd rather throw than loop forever.
        for (var i = 0; i < 1000 && taken.Contains(candidate); i++)
        {
            candidate = BumpMinorVersion(candidate);
        }
        return candidate;
    }

    /// <summary>
    /// Bumps the minor component of a "vMAJOR.MINOR" tag. "v0.1" → "v0.2",
    /// "v1.4" → "v1.5". If the input doesn't parse, falls back to "v0.1".
    /// </summary>
    private static string BumpMinorVersion(string current)
    {
        if (string.IsNullOrWhiteSpace(current)) return "v0.1";
        var trimmed = current.StartsWith("v", StringComparison.OrdinalIgnoreCase)
            ? current.Substring(1)
            : current;
        var parts = trimmed.Split('.');
        if (parts.Length < 2 || !int.TryParse(parts[0], out var major) || !int.TryParse(parts[1], out var minor))
        {
            return "v0.1";
        }
        return $"v{major}.{minor + 1}";
    }

    internal static IReadOnlyList<Guid> ParseGuidArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<Guid>();
        try
        {
            return JsonSerializer.Deserialize<List<Guid>>(json) ?? new List<Guid>();
        }
        catch (JsonException)
        {
            return Array.Empty<Guid>();
        }
    }

    private static double ComputeSuccessRate(IReadOnlyList<WorkflowRun> runs)
    {
        if (runs.Count == 0) return 0.0;
        var success = runs.Count(r => r.Status == WorkflowRunStatuses.Success);
        return (double)success / runs.Count;
    }

    private static int ComputeAvgMs(IReadOnlyList<WorkflowRun> runs)
    {
        var completed = runs.Where(r => r.DurationMs > 0).ToList();
        if (completed.Count == 0) return 0;
        return (int)completed.Average(r => r.DurationMs);
    }

    private static string FormatDuration(int ms)
    {
        if (ms <= 0) return "—";
        if (ms < 1000) return $"{ms}ms";
        if (ms < 60_000) return $"{ms / 1000.0:F1}s";
        if (ms < 3_600_000) return $"{ms / 60_000}m";
        return $"{ms / 3_600_000.0:F1}h";
    }

    /// <summary>
    /// Compares version tag strings in natural numeric order: "v1.4" &gt;
    /// "v1.3" &gt; "v1.1" &gt; "v0.9". Pulls digit runs and compares them
    /// numerically rather than lexicographically (so "v1.10" sorts above
    /// "v1.2", not below).
    /// </summary>
    private sealed class NaturalVersionComparer : IComparer<string>
    {
        public static readonly NaturalVersionComparer Instance = new();

        public int Compare(string? x, string? y)
        {
            if (string.IsNullOrEmpty(x) && string.IsNullOrEmpty(y)) return 0;
            if (string.IsNullOrEmpty(x)) return -1;
            if (string.IsNullOrEmpty(y)) return 1;

            int i = 0, j = 0;
            while (i < x.Length && j < y.Length)
            {
                if (char.IsDigit(x[i]) && char.IsDigit(y[j]))
                {
                    var iEnd = i;
                    while (iEnd < x.Length && char.IsDigit(x[iEnd])) iEnd++;
                    var jEnd = j;
                    while (jEnd < y.Length && char.IsDigit(y[jEnd])) jEnd++;
                    var xNum = long.Parse(x.AsSpan(i, iEnd - i));
                    var yNum = long.Parse(y.AsSpan(j, jEnd - j));
                    if (xNum != yNum) return xNum.CompareTo(yNum);
                    i = iEnd;
                    j = jEnd;
                }
                else
                {
                    var cmp = x[i].CompareTo(y[j]);
                    if (cmp != 0) return cmp;
                    i++;
                    j++;
                }
            }
            return x.Length.CompareTo(y.Length);
        }
    }

    private static string FormatRelative(DateTime now, DateTime past)
    {
        var diff = now - past;
        if (diff.TotalSeconds < 60) return "just now";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
        if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
        if (diff.TotalDays < 30) return $"{(int)(diff.TotalDays / 7)}w ago";
        if (diff.TotalDays < 365) return $"{(int)(diff.TotalDays / 30)} mo ago";
        return $"{(int)(diff.TotalDays / 365)}y ago";
    }
}
