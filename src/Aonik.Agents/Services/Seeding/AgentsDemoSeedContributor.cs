using System.Text.Json;
using Aonik.Agents.Entities;
using Aonik.Agents.Entities.Workflows;
using Aonik.Agents.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Persistence;
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
            DemoSeedPhase.Activity => await SeedAgentActivityAsync(context, cancellationToken),
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
        // The seed catalog uses deterministic ids for nodes/edges/comments/
        // versions/runs (and re-seeds need to land the same Guids). A plain
        // RemoveRange would be intercepted by the audit hook and turned
        // into a soft-delete (IsDeleted=true), leaving the row in place —
        // the next insert with the same Guid would then PK-violate.
        // ExecuteDeleteAsync issues a hard SQL DELETE and bypasses the
        // audit hook. IgnoreQueryFilters is included so previously
        // soft-deleted rows from older versions of this contributor are
        // also wiped clean.
        await _dbContext.WorkflowNodes
            .IncludeSoftDeleted()
            .Where(n => n.WorkflowId == workflowId)
            .ExecuteDeleteAsync(cancellationToken);

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
        await _dbContext.WorkflowEdges
            .IncludeSoftDeleted()
            .Where(e => e.WorkflowId == workflowId)
            .ExecuteDeleteAsync(cancellationToken);

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
        await _dbContext.WorkflowComments
            .IncludeSoftDeleted()
            .Where(c => c.WorkflowId == workflowId)
            .ExecuteDeleteAsync(cancellationToken);

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
        await _dbContext.WorkflowVersions
            .IncludeSoftDeleted()
            .Where(v => v.WorkflowId == workflowId)
            .ExecuteDeleteAsync(cancellationToken);

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
        await _dbContext.WorkflowRuns
            .IncludeSoftDeleted()
            .Where(r => r.WorkflowId == workflowId)
            .ExecuteDeleteAsync(cancellationToken);

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

    // ── Phase: Activity (AgentRuns + Proposals) ────────────────────────
    //
    // Seeds ~30 AgentRun rows across the seven domain agents (mix of
    // Success / Failed / InProgress, spread across the last 24h) and
    // 6 Proposals (mix of Proposed / Approved / Rejected). Re-seeding is
    // idempotent — the contributor wipes prior agent activity for this
    // tenant before reinserting.

    private async Task<IReadOnlyList<string>> SeedAgentActivityAsync(
        DemoSeedContext context,
        CancellationToken cancellationToken)
    {
        var operations = new List<string>();

        // Resolve agents we seeded in the Workflows phase.
        var agents = await _dbContext.Agents
            .Where(a => a.TenantId == context.TenantId)
            .ToListAsync(cancellationToken);

        if (agents.Count == 0)
        {
            // Workflows phase didn't seed any agents — nothing to attach
            // runs/proposals to. (E.g. test seeds that skipped earlier
            // phases.) Bail rather than orphaning rows.
            return operations;
        }

        var agentRunIds = await SeedAgentRunsAsync(context, agents, operations, cancellationToken);
        var proposalIds = await SeedProposalsAsync(context, agents, agentRunIds, operations, cancellationToken);

        _results[DemoSeedResultKeys.AgentRunIds] = agentRunIds.ToArray();
        _results[DemoSeedResultKeys.ProposalIds] = proposalIds.ToArray();
        return operations;
    }

    private async Task<IReadOnlyList<Guid>> SeedAgentRunsAsync(
        DemoSeedContext context,
        IReadOnlyList<Agent> agents,
        List<string> operations,
        CancellationToken cancellationToken)
    {
        // Hard-delete prior demo runs so re-seeds don't accumulate. Plain
        // RemoveRange would be soft-deleted by the audit hook, leaving
        // ghost rows that re-seeds would PK-conflict with. ExecuteDelete
        // bypasses the audit hook.
        await _dbContext.AgentRuns
            .IncludeSoftDeleted()
            .Where(r => r.TenantId == context.TenantId)
            .ExecuteDeleteAsync(cancellationToken);

        var runIds = new List<Guid>();
        var rng = new Random(unchecked(context.TenantId.GetHashCode() ^ 0x5eed));

        // ~5 runs per agent, jittered timestamps over the last 24h.
        var goalsByAgent = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Billing"]    = new[] { "Reconcile bank transaction tx_9f2c1a", "Score invoice match for INV-2041", "Apply payment to AR-1200" },
            ["Ledger"]     = new[] { "Post period accruals", "Run intercompany elimination", "Lock April period" },
            ["FX"]         = new[] { "Quote forward NGN→GBP", "Revalue GBP holdings", "Refresh WMR fixings" },
            ["Compliance"] = new[] { "Re-screen Primrose Logistics against UK sanctions", "Open KYC case for Naledi Dlamini", "Review high-risk transfer NG-GB" },
            ["Dunning"]    = new[] { "Compose 14-day overdue reminder for INV-2018", "Escalate dunning tier on Acme Imports", "Schedule phone hand-off for >21d invoices" },
            ["Close"]      = new[] { "Sequence April close playbook", "Verify intercompany balances", "Generate close package" },
            ["Insights"]   = new[] { "Detect spend anomaly in Fuel category", "Summarise cash position", "Narrate weekly variance" },
        };

        foreach (var agent in agents)
        {
            if (!goalsByAgent.TryGetValue(agent.Name, out var goals)) continue;

            for (var i = 0; i < 5; i++)
            {
                var minutesAgo = rng.Next(15, 24 * 60);
                var status = rng.NextDouble() switch
                {
                    < 0.78 => "Success",
                    < 0.92 => "Failed",
                    _      => "InProgress",
                };

                var run = new AgentRun
                {
                    Id = Guid.NewGuid(),
                    TenantId = context.TenantId,
                    AgentId = agent.Id,
                    Goal = goals[i % goals.Length],
                    PlanJson = "{\"steps\":[]}",
                    StepsJson = "[]",
                    LinkedAiRunIdsJson = "[]",
                    ArtifactsProducedJson = "[]",
                    Status = status,
                    CreatedAt = context.Now.AddMinutes(-minutesAgo),
                    CreatedBy = context.UserId,
                };
                _dbContext.AgentRuns.Add(run);
                runIds.Add(run.Id);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        operations.Add($"Seeded {runIds.Count} agent runs across {agents.Count} agents");
        return runIds;
    }

    private async Task<IReadOnlyList<Guid>> SeedProposalsAsync(
        DemoSeedContext context,
        IReadOnlyList<Agent> agents,
        IReadOnlyList<Guid> runIds,
        List<string> operations,
        CancellationToken cancellationToken)
    {
        await _dbContext.Proposals
            .IncludeSoftDeleted()
            .Where(p => p.TenantId == context.TenantId)
            .ExecuteDeleteAsync(cancellationToken);

        if (runIds.Count == 0) return Array.Empty<Guid>();

        var billing    = agents.FirstOrDefault(a => a.Name == "Billing");
        var compliance = agents.FirstOrDefault(a => a.Name == "Compliance");
        var fx         = agents.FirstOrDefault(a => a.Name == "FX");
        var insights   = agents.FirstOrDefault(a => a.Name == "Insights");

        var proposalSeeds = new List<ProposalSeed>();

        if (billing != null)
        {
            proposalSeeds.Add(new ProposalSeed(
                "BillingMatchApply", billing.Id,
                "Match INV-2041 (£12,480) to bank txn 9f2c1a from Primrose Logistics. Reference, amount and counterparty all align — confidence 0.94.",
                "Low", 0.94m, ProposalStatus.Proposed,
                "{\"invoiceId\":\"INV-2041\",\"txnId\":\"tx_9f2c1a\",\"amount\":12480.00}",
                context.Now.AddMinutes(-12)));
        }

        if (fx != null)
        {
            proposalSeeds.Add(new ProposalSeed(
                "FxForwardQuote", fx.Id,
                "Quote 1-month GBP→NGN forward at ₦2,012. Locks rate for cross-border invoices over the next 30 days.",
                "Medium", 0.88m, ProposalStatus.Proposed,
                "{\"corridor\":\"GBP-NGN\",\"tenor\":\"1M\",\"rate\":2012}",
                context.Now.AddHours(-2)));
        }

        if (compliance != null)
        {
            proposalSeeds.Add(new ProposalSeed(
                "ComplianceCaseOpen", compliance.Id,
                "Open KYB review on Naledi Dlamini after risk score moved above 0.6 on the latest sanctions screening.",
                "High", 0.91m, ProposalStatus.Proposed,
                "{\"partyName\":\"Naledi Dlamini\",\"riskScore\":0.62}",
                context.Now.AddHours(-4)));
        }

        if (billing != null)
        {
            proposalSeeds.Add(new ProposalSeed(
                "DunningReminder", billing.Id,
                "Send 14-day overdue reminder to Acme Imports Ltd for INV-2018 (£1,200). Already chased once at the 7-day mark.",
                "Low", 0.82m, ProposalStatus.Approved,
                "{\"invoiceId\":\"INV-2018\",\"daysOverdue\":14}",
                context.Now.AddDays(-1),
                ApprovedAt: context.Now.AddHours(-22)));
        }

        if (insights != null)
        {
            proposalSeeds.Add(new ProposalSeed(
                "InsightsAnomalyAlert", insights.Id,
                "Spend anomaly: Fuel category up 47% on the 30-day rolling average. Driver fleet running on weekend trips this week.",
                "Low", 0.76m, ProposalStatus.Approved,
                "{\"category\":\"Fuel\",\"deltaPct\":47}",
                context.Now.AddDays(-3),
                ApprovedAt: context.Now.AddDays(-3).AddHours(2)));
        }

        if (compliance != null)
        {
            proposalSeeds.Add(new ProposalSeed(
                "ComplianceCaseOpen", compliance.Id,
                "Open KYB review on Safari Freight Co after registration number drift detected.",
                "High", 0.71m, ProposalStatus.Rejected,
                "{\"partyName\":\"Safari Freight Co\",\"reason\":\"name_match_only\"}",
                context.Now.AddDays(-5),
                ApprovedAt: context.Now.AddDays(-5).AddHours(1)));
        }

        var proposalIds = new List<Guid>();
        foreach (var seed in proposalSeeds)
        {
            var proposal = new Proposal
            {
                Id = Guid.NewGuid(),
                TenantId = context.TenantId,
                ProposalType = seed.ProposalType,
                ProposedByAgentId = seed.ProposedByAgentId,
                AiRunId = runIds[Math.Abs(seed.GetHashCode()) % runIds.Count],
                ImpactSummary = seed.ImpactSummary,
                RiskTier = seed.RiskTier,
                Confidence = seed.Confidence,
                Status = seed.Status,
                PayloadJson = seed.PayloadJson,
                ApprovedAt = seed.ApprovedAt,
                ApprovedByUserId = seed.Status == ProposalStatus.Approved ? context.UserId : null,
                CreatedAt = seed.CreatedAt,
                CreatedBy = context.UserId,
            };
            _dbContext.Proposals.Add(proposal);
            proposalIds.Add(proposal.Id);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        operations.Add($"Seeded {proposalIds.Count} proposals");
        return proposalIds;
    }

    private sealed record ProposalSeed(
        string ProposalType,
        Guid ProposedByAgentId,
        string ImpactSummary,
        string RiskTier,
        decimal Confidence,
        ProposalStatus Status,
        string PayloadJson,
        DateTime CreatedAt,
        DateTime? ApprovedAt = null);

    private sealed record AgentSeed(
        string Name,
        string Description,
        string Domain,
        string OwnerColor,
        string RiskTier);
}
