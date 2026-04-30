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

    public async Task<IReadOnlyList<WorkflowVersionResponse>> ListVersionsAsync(
        Guid workflowId,
        CancellationToken cancellationToken = default)
    {
        var versions = await _dbContext.WorkflowVersions
            .AsNoTracking()
            .Where(v => v.WorkflowId == workflowId)
            .OrderByDescending(v => v.CreatedAt)
            .ToListAsync(cancellationToken);

        var now = _clock.UtcNow;

        return versions.Select(v => new WorkflowVersionResponse(
            Id: v.Id,
            Tag: v.Tag,
            Message: v.Message,
            AuthorName: v.AuthorName,
            AuthorColor: v.AuthorColor,
            CreatedAt: v.CreatedAt,
            When: FormatRelative(now, v.CreatedAt))).ToList();
    }

    // ── Helpers ──────────────────────────────────────────────────

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
