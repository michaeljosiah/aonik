using System.Text.Json;
using Aonik.Agents.Entities;
using Aonik.Agents.Entities.Workflows;
using Aonik.Agents.Persistence;
using Aonik.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aonik.Agents.Services.Seeding;

/// <summary>
/// Demo-seed contributor for the Agents module.
///
/// Owns the <see cref="DemoSeedPhase.Workflows"/> phase: seeds the seven
/// domain agents that the workflow registry references, then the seven
/// workflows themselves with full node graphs, edges, recent runs, and
/// version history. All inserts are idempotent — re-running the seed
/// upserts existing rows by deterministic Guid.
/// </summary>
internal sealed class AgentsDemoSeedContributor : IDemoSeedContributor
{
    private readonly AgentsDbContext _dbContext;
    private readonly ILogger<AgentsDemoSeedContributor> _logger;
    private readonly Dictionary<string, object> _results = new();

    public AgentsDemoSeedContributor(
        AgentsDbContext dbContext,
        ILogger<AgentsDemoSeedContributor> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public string ModuleName => "Agents";

    public async Task<IReadOnlyList<string>> SeedAsync(
        DemoSeedPhase phase,
        DemoSeedContext context,
        CancellationToken cancellationToken = default)
    {
        return phase switch
        {
            DemoSeedPhase.Workflows => await SeedWorkflowsAsync(context, cancellationToken),
            _ => Array.Empty<string>(),
        };
    }

    public void ClearTracking() => _dbContext.ChangeTracker.Clear();

    public IReadOnlyDictionary<string, object> GetResults() => _results;

    // ── Phase: Workflows ────────────────────────────────────────────────

    private async Task<IReadOnlyList<string>> SeedWorkflowsAsync(
        DemoSeedContext context,
        CancellationToken cancellationToken)
    {
        var operations = new List<string>();

        // 1. Seven domain agents the workflows reference. Tenant-scoped
        //    rows so re-running the seed against another tenant produces
        //    a fresh fleet rather than reusing global rows.
        var agentIdsByName = await UpsertAgentFleetAsync(context, operations, cancellationToken);

        // 2. Seven workflows from the starterkit template, fully expanded
        //    (header + nodes + edges + comments + versions + recent runs).
        var workflowIdsBySlug = await UpsertWorkflowsAsync(context, agentIdsByName, operations, cancellationToken);

        _results[DemoSeedResultKeys.AgentIdsByName] = agentIdsByName;
        _results[DemoSeedResultKeys.WorkflowIdsBySlug] = workflowIdsBySlug;

        return operations;
    }

    private async Task<IReadOnlyDictionary<string, Guid>> UpsertAgentFleetAsync(
        DemoSeedContext context,
        List<string> operations,
        CancellationToken cancellationToken)
    {
        var seeds = new[]
        {
            new AgentSeed("Billing",    "Reconciliation, dunning, AR ageing.",         "Finance",     "#eb5c37", "Low"),
            new AgentSeed("Ledger",     "Journal entries, intercompany, period close.", "Finance",    "#055a60", "Medium"),
            new AgentSeed("FX",         "FX quoting, hedging, treasury exposure.",     "Finance",     "#3ab795", "Medium"),
            new AgentSeed("Compliance", "KYC, sanctions screening, case review.",      "Risk",        "#7b76b6", "High"),
            new AgentSeed("Dunning",    "Customer-facing reminders and tone control.", "Customer",    "#5facbd", "Low"),
            new AgentSeed("Close",      "Month-end close playbook orchestration.",     "Finance",     "#0097a9", "High"),
            new AgentSeed("Insights",   "Spend anomalies, narrative summaries.",       "Insights",    "#d4a843", "Low"),
        };

        var idsByName = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        foreach (var seed in seeds)
        {
            // Tenant-scoped lookup — global agents from IDomainAgentDescriptor
            // seeding live alongside but with TenantId == null and are not
            // overwritten here.
            var existing = await _dbContext.Agents
                .FirstOrDefaultAsync(
                    a => a.TenantId == context.TenantId && a.Name == seed.Name,
                    cancellationToken);

            if (existing is null)
            {
                existing = new Agent
                {
                    Id = Guid.NewGuid(),
                    TenantId = context.TenantId,
                    Name = seed.Name,
                    Description = seed.Description,
                    Domain = seed.Domain,
                    InstructionsText = $"Demo {seed.Name} agent. Seeded for the workflows registry.",
                    InstructionPromptSpecId = Guid.Empty,
                    ToolsetIdsJson = "[]",
                    InputSchemaJson = "{}",
                    OutputSchemaJson = "{}",
                    PermissionsProfileJson = "{}",
                    RiskTier = seed.RiskTier,
                    AgentType = AgentType.SubAgent,
                    IsActive = true,
                    IconUrl = null,
                    CreatedAt = context.Now,
                    CreatedBy = context.UserId,
                };
                _dbContext.Agents.Add(existing);
                operations.Add($"Seeded agent {seed.Name}");
            }
            else
            {
                existing.Description = seed.Description;
                existing.Domain = seed.Domain;
                existing.RiskTier = seed.RiskTier;
                existing.IsActive = true;
                existing.UpdatedAt = context.Now;
                existing.UpdatedBy = context.UserId;
            }

            idsByName[seed.Name] = existing.Id;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return idsByName;
    }

    private async Task<IReadOnlyDictionary<string, Guid>> UpsertWorkflowsAsync(
        DemoSeedContext context,
        IReadOnlyDictionary<string, Guid> agentIdsByName,
        List<string> operations,
        CancellationToken cancellationToken)
    {
        var seeds = WorkflowSeedCatalog.Build(agentIdsByName, context.Now);
        var idsBySlug = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        foreach (var seed in seeds)
        {
            var workflow = await UpsertWorkflowAsync(context, seed, cancellationToken);
            idsBySlug[seed.Slug] = workflow.Id;

            await UpsertNodesAsync(context, workflow.Id, seed.Nodes, cancellationToken);
            await UpsertEdgesAsync(context, workflow.Id, seed.Edges, cancellationToken);
            await UpsertCommentsAsync(context, workflow.Id, seed.Comments, cancellationToken);
            await UpsertVersionsAsync(context, workflow.Id, seed.Versions, cancellationToken);
            await UpsertRunsAsync(context, workflow.Id, seed.Runs, cancellationToken);

            operations.Add($"Seeded workflow {seed.Slug}");
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return idsBySlug;
    }

    private async Task<Workflow> UpsertWorkflowAsync(
        DemoSeedContext context,
        WorkflowSeed seed,
        CancellationToken cancellationToken)
    {
        var existing = await _dbContext.Workflows
            .FirstOrDefaultAsync(
                w => w.TenantId == context.TenantId && w.Slug == seed.Slug,
                cancellationToken);

        var contributorsJson = JsonSerializer.Serialize(seed.ContributorAgentIds);

        if (existing is null)
        {
            existing = new Workflow
            {
                Id = seed.WorkflowId,
                TenantId = context.TenantId,
                Slug = seed.Slug,
                Name = seed.Name,
                Description = seed.Description,
                OwnerAgentId = seed.OwnerAgentId,
                OwnerColor = seed.OwnerColor,
                ContributorsJson = contributorsJson,
                State = seed.State,
                Version = seed.Version,
                AutoRetry = seed.AutoRetry,
                TriggerCount = seed.TriggerCount,
                CreatedAt = context.Now,
                CreatedBy = context.UserId,
            };
            _dbContext.Workflows.Add(existing);
            return existing;
        }

        existing.Name = seed.Name;
        existing.Description = seed.Description;
        existing.OwnerAgentId = seed.OwnerAgentId;
        existing.OwnerColor = seed.OwnerColor;
        existing.ContributorsJson = contributorsJson;
        existing.State = seed.State;
        existing.Version = seed.Version;
        existing.AutoRetry = seed.AutoRetry;
        existing.TriggerCount = seed.TriggerCount;
        existing.UpdatedAt = context.Now;
        existing.UpdatedBy = context.UserId;
        return existing;
    }

    private async Task UpsertNodesAsync(
        DemoSeedContext context,
        Guid workflowId,
        IReadOnlyList<WorkflowNodeSeed> seeds,
        CancellationToken cancellationToken)
    {
        // The seed catalog uses deterministic node ids (n1, n2, …) for edge
        // wiring. Strategy: blow away existing rows for this workflow and
        // re-insert. Cheap for demo volumes and side-steps the
        // version-token / soft-delete dance for re-seeds.
        var existing = await _dbContext.WorkflowNodes
            .Where(n => n.WorkflowId == workflowId)
            .ToListAsync(cancellationToken);
        if (existing.Count > 0) _dbContext.WorkflowNodes.RemoveRange(existing);

        foreach (var seed in seeds)
        {
            _dbContext.WorkflowNodes.Add(new WorkflowNode
            {
                Id = seed.NodeId,
                TenantId = context.TenantId,
                WorkflowId = workflowId,
                Kind = seed.Kind,
                Label = seed.Label,
                Summary = seed.Summary ?? string.Empty,
                Notes = string.Empty,
                X = seed.X,
                Y = seed.Y,
                ParamsJson = seed.ParamsJson ?? "{}",
                CreatedAt = context.Now,
                CreatedBy = context.UserId,
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task UpsertEdgesAsync(
        DemoSeedContext context,
        Guid workflowId,
        IReadOnlyList<WorkflowEdgeSeed> seeds,
        CancellationToken cancellationToken)
    {
        var existing = await _dbContext.WorkflowEdges
            .Where(e => e.WorkflowId == workflowId)
            .ToListAsync(cancellationToken);
        if (existing.Count > 0) _dbContext.WorkflowEdges.RemoveRange(existing);

        foreach (var seed in seeds)
        {
            _dbContext.WorkflowEdges.Add(new WorkflowEdge
            {
                Id = seed.EdgeId,
                TenantId = context.TenantId,
                WorkflowId = workflowId,
                FromNodeId = seed.FromNodeId,
                ToNodeId = seed.ToNodeId,
                FromIndex = seed.FromIndex,
                Label = seed.Label ?? string.Empty,
                CreatedAt = context.Now,
                CreatedBy = context.UserId,
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task UpsertCommentsAsync(
        DemoSeedContext context,
        Guid workflowId,
        IReadOnlyList<WorkflowCommentSeed> seeds,
        CancellationToken cancellationToken)
    {
        var existing = await _dbContext.WorkflowComments
            .Where(c => c.WorkflowId == workflowId)
            .ToListAsync(cancellationToken);
        if (existing.Count > 0) _dbContext.WorkflowComments.RemoveRange(existing);

        foreach (var seed in seeds)
        {
            _dbContext.WorkflowComments.Add(new WorkflowComment
            {
                Id = seed.CommentId,
                TenantId = context.TenantId,
                WorkflowId = workflowId,
                X = seed.X,
                Y = seed.Y,
                Author = seed.Author,
                Body = seed.Body,
                CreatedAt = context.Now,
                CreatedBy = context.UserId,
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task UpsertVersionsAsync(
        DemoSeedContext context,
        Guid workflowId,
        IReadOnlyList<WorkflowVersionSeed> seeds,
        CancellationToken cancellationToken)
    {
        var existing = await _dbContext.WorkflowVersions
            .Where(v => v.WorkflowId == workflowId)
            .ToListAsync(cancellationToken);
        if (existing.Count > 0) _dbContext.WorkflowVersions.RemoveRange(existing);

        foreach (var seed in seeds)
        {
            _dbContext.WorkflowVersions.Add(new WorkflowVersion
            {
                Id = seed.VersionId,
                TenantId = context.TenantId,
                WorkflowId = workflowId,
                Tag = seed.Tag,
                Message = seed.Message,
                AuthorName = seed.AuthorName,
                AuthorColor = seed.AuthorColor,
                CreatedAt = seed.CreatedAt,
                CreatedBy = context.UserId,
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task UpsertRunsAsync(
        DemoSeedContext context,
        Guid workflowId,
        IReadOnlyList<WorkflowRunSeed> seeds,
        CancellationToken cancellationToken)
    {
        var existing = await _dbContext.WorkflowRuns
            .Where(r => r.WorkflowId == workflowId)
            .ToListAsync(cancellationToken);
        if (existing.Count > 0) _dbContext.WorkflowRuns.RemoveRange(existing);

        foreach (var seed in seeds)
        {
            _dbContext.WorkflowRuns.Add(new WorkflowRun
            {
                Id = seed.RunId,
                TenantId = context.TenantId,
                WorkflowId = workflowId,
                StartedAt = seed.StartedAt,
                CompletedAt = seed.CompletedAt,
                Status = seed.Status,
                DurationMs = seed.DurationMs,
                StartedBy = seed.StartedBy,
                SequenceJson = JsonSerializer.Serialize(seed.Sequence),
                CreatedAt = seed.StartedAt,
                CreatedBy = context.UserId,
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private sealed record AgentSeed(
        string Name,
        string Description,
        string Domain,
        string OwnerColor,
        string RiskTier);
}
