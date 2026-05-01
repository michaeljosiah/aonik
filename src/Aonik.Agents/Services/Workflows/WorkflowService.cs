using System.Text.Json;
using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Contracts.Services;
using Aonik.Agents.Entities;
using Aonik.Agents.Entities.Workflows;
using Aonik.Agents.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Agents.Services.Workflows;

/// <summary>
/// Read-only workflow registry service. Materialises the list-page summary
/// + KPI aggregates from <see cref="WorkflowRun"/> rows inline rather than
/// caching them, which is fine for the demo data volumes; if/when the run
/// table grows the per-workflow stat block is the obvious thing to lift
/// into a denormalised projection.
/// </summary>
internal sealed class WorkflowService : IWorkflowService
{
    private readonly AgentsDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IClock _clock;

    public WorkflowService(
        AgentsDbContext dbContext,
        ITenantProvider tenantProvider,
        IClock clock)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _clock = clock;
    }

    public async Task<IReadOnlyList<WorkflowSummaryResponse>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        var workflows = await _dbContext.Workflows
            .AsNoTracking()
            .OrderByDescending(w => w.UpdatedAt ?? w.CreatedAt)
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
                State: w.State,
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
        var workflow = await _dbContext.Workflows
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Slug == slug, cancellationToken);

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

        return new WorkflowGraphResponse(
            Id: workflow.Id,
            Slug: workflow.Slug,
            Name: workflow.Name,
            Description: workflow.Description,
            State: workflow.State,
            Version: workflow.Version,
            AutoRetry: workflow.AutoRetry,
            OwnerColor: workflow.OwnerColor,
            OwnerName: ownerName,
            Contributors: contributorNames,
            Nodes: nodes.Select(n => new WorkflowGraphNode(
                n.Id, n.Kind, n.Label, n.Summary, n.Notes, n.X, n.Y, n.ParamsJson)).ToList(),
            Edges: edges.Select(e => new WorkflowGraphEdge(
                e.Id, e.FromNodeId, e.ToNodeId, e.FromIndex, e.Label)).ToList(),
            Comments: comments.Select(c => new WorkflowGraphComment(
                c.Id, c.X, c.Y, c.Author, c.Body)).ToList());
    }

    public async Task<IReadOnlyList<WorkflowRunResponse>> ListRunsAsync(
        Guid workflowId,
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        var runs = await _dbContext.WorkflowRuns
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

    public async Task<WorkflowGraphResponse> SaveAsync(
        WorkflowSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Slug))
        {
            throw new ArgumentException("Slug is required.", nameof(request));
        }
        if (request.Nodes.Count == 0)
        {
            throw new ArgumentException("Workflow must have at least one node.", nameof(request));
        }

        ValidateGraph(request);

        var existing = await _dbContext.Workflows
            .FirstOrDefaultAsync(w => w.Slug == request.Slug, cancellationToken);

        var contributorsJson = JsonSerializer.Serialize(request.Contributors);

        Workflow workflow;
        bool isNew = existing is null;

        if (existing is null)
        {
            workflow = new Workflow
            {
                Slug = request.Slug,
                Name = request.Name,
                Description = request.Description ?? string.Empty,
                State = string.IsNullOrWhiteSpace(request.State) ? WorkflowStates.Draft : request.State,
                Version = string.IsNullOrWhiteSpace(request.Version) ? "v0.1" : request.Version,
                AutoRetry = request.AutoRetry,
                OwnerColor = request.OwnerColor ?? string.Empty,
                OwnerAgentId = request.OwnerAgentId,
                ContributorsJson = contributorsJson,
            };
            _dbContext.Workflows.Add(workflow);
        }
        else
        {
            // Each save bumps the minor version and stamps a new
            // WorkflowVersion audit row. We pick a tag that doesn't
            // already exist for this workflow — the seed catalog can
            // pre-populate version rows (e.g. v1.4 already exists for
            // match_and_apply), so a naive bump can collide.
            var newVersion = await NextAvailableVersionAsync(existing, cancellationToken);
            await SnapshotVersionAsync(existing, newVersion, request.VersionMessage, cancellationToken);

            existing.Name = request.Name;
            existing.Description = request.Description ?? string.Empty;
            existing.State = string.IsNullOrWhiteSpace(request.State) ? existing.State : request.State;
            existing.Version = newVersion;
            existing.AutoRetry = request.AutoRetry;
            existing.OwnerColor = request.OwnerColor ?? existing.OwnerColor;
            existing.OwnerAgentId = request.OwnerAgentId ?? existing.OwnerAgentId;
            existing.ContributorsJson = contributorsJson;
            workflow = existing;

            // Replace nodes + edges fully — diffing isn't worth the
            // complexity for the graph sizes we have.
            var oldNodes = await _dbContext.WorkflowNodes
                .Where(n => n.WorkflowId == existing.Id)
                .ToListAsync(cancellationToken);
            var oldEdges = await _dbContext.WorkflowEdges
                .Where(e => e.WorkflowId == existing.Id)
                .ToListAsync(cancellationToken);
            _dbContext.WorkflowNodes.RemoveRange(oldNodes);
            _dbContext.WorkflowEdges.RemoveRange(oldEdges);
        }

        // Build the new node + edge rows. Map client ids → fresh Guids
        // so edges can find their endpoints regardless of whether the
        // client used a server Guid or an editor-local id.
        var clientToGuid = new Dictionary<string, Guid>(request.Nodes.Count, StringComparer.Ordinal);
        var newNodes = new List<WorkflowNode>(request.Nodes.Count);
        foreach (var n in request.Nodes)
        {
            var id = Guid.NewGuid();
            clientToGuid[n.ClientId] = id;
            newNodes.Add(new WorkflowNode
            {
                Id = id,
                WorkflowId = workflow.Id,
                Kind = n.Kind ?? string.Empty,
                Label = n.Label ?? string.Empty,
                Summary = n.Summary ?? string.Empty,
                Notes = n.Notes ?? string.Empty,
                X = n.X,
                Y = n.Y,
                ParamsJson = string.IsNullOrWhiteSpace(n.ParamsJson) ? "{}" : n.ParamsJson,
            });
        }
        _dbContext.WorkflowNodes.AddRange(newNodes);

        var newEdges = new List<WorkflowEdge>(request.Edges.Count);
        foreach (var e in request.Edges)
        {
            if (!clientToGuid.TryGetValue(e.FromClientId, out var fromId)
                || !clientToGuid.TryGetValue(e.ToClientId, out var toId))
            {
                // Edge references a node that wasn't in the request — drop
                // it rather than leaving an orphan row. Validation should
                // have caught this earlier; this is a safety net.
                continue;
            }
            newEdges.Add(new WorkflowEdge
            {
                Id = Guid.NewGuid(),
                WorkflowId = workflow.Id,
                FromNodeId = fromId,
                ToNodeId = toId,
                FromIndex = e.FromIndex,
                Label = e.Label ?? string.Empty,
            });
        }
        _dbContext.WorkflowEdges.AddRange(newEdges);

        await _dbContext.SaveChangesAsync(cancellationToken);

        var graph = await GetBySlugAsync(request.Slug, cancellationToken);
        return graph!;

        // Local helper — keeps SaveAsync readable.
        static void ValidateGraph(WorkflowSaveRequest req)
        {
            var triggers = req.Nodes.Count(n => string.Equals(n.Kind, "trigger", StringComparison.OrdinalIgnoreCase));
            if (triggers != 1)
            {
                throw new ArgumentException(
                    $"Workflow must have exactly one trigger node (found {triggers}).",
                    nameof(req));
            }

            var clientIds = new HashSet<string>(req.Nodes.Select(n => n.ClientId), StringComparer.Ordinal);
            foreach (var edge in req.Edges)
            {
                if (!clientIds.Contains(edge.FromClientId) || !clientIds.Contains(edge.ToClientId))
                {
                    throw new ArgumentException(
                        "Edge references a node id that is not present in the request.",
                        nameof(req));
                }
            }
        }
    }

    public async Task<bool> DeleteAsync(string slug, CancellationToken cancellationToken = default)
    {
        var workflow = await _dbContext.Workflows
            .FirstOrDefaultAsync(w => w.Slug == slug, cancellationToken);
        if (workflow is null) return false;

        // Soft-delete the workflow + its graph children. Runs and
        // version history are preserved as historical record.
        workflow.IsDeleted = true;
        workflow.DeletedAt = _clock.UtcNow;

        var nodes = await _dbContext.WorkflowNodes
            .Where(n => n.WorkflowId == workflow.Id)
            .ToListAsync(cancellationToken);
        var edges = await _dbContext.WorkflowEdges
            .Where(e => e.WorkflowId == workflow.Id)
            .ToListAsync(cancellationToken);
        var comments = await _dbContext.WorkflowComments
            .Where(c => c.WorkflowId == workflow.Id)
            .ToListAsync(cancellationToken);

        var now = _clock.UtcNow;
        foreach (var n in nodes) { n.IsDeleted = true; n.DeletedAt = now; }
        foreach (var e in edges) { e.IsDeleted = true; e.DeletedAt = now; }
        foreach (var c in comments) { c.IsDeleted = true; c.DeletedAt = now; }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<WorkflowVersionResponse>> ListVersionsAsync(
        Guid workflowId,
        CancellationToken cancellationToken = default)
    {
        var versions = await _dbContext.WorkflowVersions
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
            When: FormatRelative(now, v.CreatedAt))).ToList();
    }

    // ── Helpers ──────────────────────────────────────────────────

    private async Task SnapshotVersionAsync(
        Workflow workflow,
        string tag,
        string? message,
        CancellationToken cancellationToken)
    {
        var snapshot = new WorkflowVersion
        {
            WorkflowId = workflow.Id,
            Tag = tag,
            Message = string.IsNullOrWhiteSpace(message) ? "Saved by editor." : message!,
            AuthorName = "Editor",
            AuthorColor = workflow.OwnerColor,
        };
        _dbContext.WorkflowVersions.Add(snapshot);
        await Task.CompletedTask;
    }

    /// <summary>
    /// Picks the next "vMAJOR.MINOR" version tag that doesn't yet have
    /// a <see cref="WorkflowVersion"/> row for this workflow. Walks from
    /// the existing tag forward — most saves only bump by one, but the
    /// seed can pre-populate intermediate tags so we keep stepping past
    /// any collisions instead of failing the save.
    /// </summary>
    private async Task<string> NextAvailableVersionAsync(
        Workflow workflow,
        CancellationToken cancellationToken)
    {
        var existingTags = await _dbContext.WorkflowVersions
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

    private static IReadOnlyList<Guid> ParseGuidArray(string? json)
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
