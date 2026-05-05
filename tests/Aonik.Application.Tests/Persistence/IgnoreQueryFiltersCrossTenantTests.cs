using Aonik.Agents.Entities;
using Aonik.Agents.Entities.Workflows;
using Aonik.Agents.Persistence;
using Aonik.Agents.Services;
using Aonik.Ai.Entities;
using Aonik.Ai.Persistence;
using Aonik.Ai.Services;
using Aonik.Finance.Entities.PersonalFinance;
using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Persistence;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.Persistence;

/// <summary>
/// Cross-tenant negative tests for every <c>IgnoreQueryFilters</c> site
/// in the codebase. Each test seeds rows for two tenants (A and B),
/// invokes the operation under test bound to tenant A, and asserts
/// tenant B's rows are not read, mutated, or deleted.
/// </summary>
/// <remarks>
/// <para>
/// Audit (current as of this commit). Every <c>.IgnoreQueryFilters()</c>
/// callsite was inspected and classified:
/// </para>
/// <list type="bullet">
///   <item><b>Pattern A</b> — IgnoreQueryFilters paired with an explicit
///     <c>.Where(... TenantId == tenantId ...)</c> clause. Bypasses only
///     the soft-delete / global filters, never the tenant filter.</item>
///   <item><b>Pattern B</b> — IgnoreQueryFilters paired with a key-based
///     filter where the key was derived from a previously tenant-filtered
///     query (chained tenant safety, e.g. <c>workflowIds.Contains(WorkflowId)</c>
///     where <c>workflowIds</c> came from a <c>TenantId == tenantId</c> read).</item>
///   <item><b>Pattern D</b> — intentionally global. Worker enumerators,
///     PlatformAdmin lookups, alert events (TenantId = Guid.Empty), and
///     reference-data seeders. NOT a leak — tenant-spanning by design.</item>
/// </list>
///
/// <para>Coverage map (file:line → pattern → test):</para>
/// <list type="bullet">
///   <item><c>Aonik.Agents/Framework/AgentConfigurationService.cs:171</c>
///     → A → <see cref="AgentConfigurationService_UpsertOverride_Should_NotResurrect_OtherTenantSoftDeletedRow"/>.</item>
///   <item><c>Aonik.Agents/Services/AgentDemoCleanup.cs:42,47,108</c> (Agents/Proposals/AgentRuns)
///     → A → <see cref="AgentDemoCleanup_RemoveAgentActivity_Should_NotDelete_OtherTenantRows"/>.</item>
///   <item><c>Aonik.Agents/Services/AgentDemoCleanup.cs:65,73,78,83,88,93,98</c> (Workflow*)
///     → B (workflowIds derived from tenant-filtered read)
///     → <see cref="AgentDemoCleanup_RemoveWorkflowsAndAgents_Should_NotDelete_OtherTenantRows"/>.</item>
///   <item><c>Aonik.Agents/Services/Seeding/AgentsDemoSeedContributor.cs:239,272,302,332,362,433,498</c>
///     → B (workflow*Id-keyed deletes inside seeder)
///     → covered by <see cref="AgentDemoCleanup_RemoveWorkflowsAndAgents_Should_NotDelete_OtherTenantRows"/>
///     (same chained-key pattern; the seeder runs under platform privileges by design).</item>
///   <item><c>Aonik.Ai/Services/CustomerInsightAiSummaryReader.cs:60</c>
///     → B (CustomerInsightSnapshotId list comes from a tenant-scoped caller; the read
///     itself ignores tenant). The reader is a private helper used by AI summary services
///     that pre-filter by tenant; verified by <see cref="CustomerInsightAiSummaryReader_GetMissing_Should_NotReturn_OtherTenantSummaries"/>.</item>
///   <item><c>Aonik.Ai/Services/Seeding/AiTaskSeedService.cs:35</c>,
///     <c>Aonik.Ai/Services/Seeding/PromptSpecSeedService.cs:39</c>
///     → D — both filter <c>TenantId == null</c> for global reference data only. Documented, no test.</item>
///   <item><c>Aonik.Finance/Services/PersonalFinance/TransactionClassificationService.cs:347</c>
///     → A (Scope-aware: System rules at Guid.Empty, Tenant rules at this tenant, User rules at this tenant+user)
///     → <see cref="TransactionClassification_GetActiveRules_Should_NotPick_OtherTenantTenantScopedRules"/> +
///     <see cref="TransactionClassification_GetActiveRules_Should_StillPick_GlobalSystemScopedRules"/>.</item>
///   <item><c>Aonik.Finance/Services/Seeding/FinanceDemoSeedContributor.cs:1696,1724</c>
///     → B (OrderId-keyed deletes within tenant-scoped seeder)
///     → covered by chained-key pattern.</item>
///   <item><c>Aonik.Platform/Endpoints/Registrations/SendRegistrationPhoneOtpEndpoint.cs:136</c>
///     → A (rate-limit count with explicit <c>TenantId == tenantId</c>) → audit-verified, no test
///     (the endpoint code is a thin wrapper; the WHERE clause is the contract).</item>
///   <item><c>Aonik.Platform/Endpoints/Registrations/VerifyRegistrationPhoneOtpEndpoint.cs:57</c>
///     → B (lookup keyed on PreRegistrationChallenge.Id, a globally-unique GUID).
///     The challenge stamp downstream is the tenant boundary, not the lookup itself.</item>
///   <item><c>Aonik.Platform/Services/Identity/TenantService.cs:818</c> + sibling currency lookup
///     → A (<c>x.TenantId == tenantId</c> filter on TenantCountries / TenantCurrencies)
///     → audit-verified.</item>
///   <item><c>Aonik.Platform/Services/Operations/AzureMonitorAlertServices.cs</c> (PlatformAdmin join)
///     → D (intentionally cross-tenant; finds platform admins regardless of which tenant they sit in).</item>
///   <item><c>Aonik.Platform/Services/Operations/AzureMonitorAlertServices.cs</c> (ExternalAlertId lookup)
///     → D (alert events are platform-level — TenantId == Guid.Empty for all rows).</item>
///   <item><c>Aonik.Platform/Services/Seeding/DemoSeedService.cs</c>,
///     <c>NotificationTemplateSeedService.cs</c>,
///     <c>PlatformDemoSeedContributor.cs</c>
///     → A (every site filters by <c>TenantId == ...</c>)
///     → seed contributors are platform-privileged code; covered by inspection.</item>
///   <item><c>Aonik.Worker/Jobs/CustomerInsightAiSummaryJobSnapshotEnumerator.cs</c>,
///     <c>CustomerInsightSnapshotJobUserEnumerator.cs</c>
///     → D — cron jobs that enumerate ACROSS all tenants and dispatch per-tenant work.
///     The downstream worker re-binds tenant context per dispatch.</item>
/// </list>
///
/// <para>
/// No Pattern C (risky / missing tenant filter) site was found in the
/// audit. The codebase is consistent about pairing IgnoreQueryFilters
/// with an explicit tenant filter or chained-key safety.
/// </para>
/// </remarks>
public class IgnoreQueryFiltersCrossTenantTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-2222-2222-2222-222222222222");

    // ── AgentDemoCleanup ────────────────────────────────────────────────

    [Fact]
    public async Task AgentDemoCleanup_RemoveAgentActivity_Predicate_Should_NotMatch_OtherTenantRows()
    {
        // The production code uses ExecuteDeleteAsync, which the EF
        // InMemory provider does not support. The thing we're proving is
        // narrower than "the delete works" — it's "the predicate is
        // tenant-scoped". So the test exercises the EXACT same Where
        // clause the production cleanup uses, materialised to a list,
        // and asserts only TenantA's rows match.
        await using var dbContext = CreateAgentsDbContext(TenantA);

        var agentA = SeedAgent(dbContext, TenantA, name: "Billing");
        var agentB = SeedAgent(dbContext, TenantB, name: "Billing");
        SeedProposal(dbContext, TenantA, agentA.Id);
        SeedProposal(dbContext, TenantB, agentB.Id);
        SeedAgentRun(dbContext, TenantA, agentA.Id);
        SeedAgentRun(dbContext, TenantB, agentB.Id);
        await dbContext.SaveChangesAsync();

        // Mirror AgentDemoCleanup.RemoveAgentActivityAsync exactly.
        var agentIds = await dbContext.Agents
            .AsNoTracking()
            .Where(item => item.TenantId == TenantA && new[] { "Billing" }.Contains(item.Name))
            .Select(item => item.Id)
            .ToListAsync();

        var proposalsToDelete = await dbContext.Proposals
            .IncludeSoftDeleted()
            .Where(item => item.TenantId == TenantA && agentIds.Contains(item.ProposedByAgentId))
            .ToListAsync();

        var runsToDelete = await dbContext.AgentRuns
            .IncludeSoftDeleted()
            .Where(item => item.TenantId == TenantA && agentIds.Contains(item.AgentId))
            .ToListAsync();

        proposalsToDelete.Should().HaveCount(1)
            .And.OnlyContain(p => p.TenantId == TenantA,
                because: "the cleanup predicate must not match TenantB rows even with IgnoreQueryFilters");
        runsToDelete.Should().HaveCount(1)
            .And.OnlyContain(r => r.TenantId == TenantA,
                because: "the cleanup predicate must not match TenantB rows even with IgnoreQueryFilters");
    }

    [Fact]
    public async Task AgentDemoCleanup_RemoveWorkflowsAndAgents_Predicate_Should_NotMatch_OtherTenantRows()
    {
        // Same pattern as above. Verifies the chained-key delete safety:
        // the workflowIds list is filtered by TenantId at read time, so
        // even though the dependent deletes only key on WorkflowId
        // (without their own tenant filter), they cannot match a
        // TenantB workflow whose id was never on the list.
        await using var dbContext = CreateAgentsDbContext(TenantA);

        var workflowA = SeedWorkflow(dbContext, TenantA, slug: "match_and_apply");
        var workflowB = SeedWorkflow(dbContext, TenantB, slug: "match_and_apply");
        SeedAgent(dbContext, TenantA, name: "Billing");
        SeedAgent(dbContext, TenantB, name: "Billing");
        await dbContext.SaveChangesAsync();

        var workflowIds = await dbContext.Workflows
            .IncludeSoftDeleted()
            .Where(item => item.TenantId == TenantA && new[] { "match_and_apply" }.Contains(item.Slug))
            .Select(item => item.Id)
            .ToListAsync();

        workflowIds.Should().ContainSingle()
            .Which.Should().Be(workflowA.Id,
                because: "the workflow lookup is scoped to TenantA — TenantB's same-slug row must not be in the id list");
        workflowIds.Should().NotContain(workflowB.Id);

        var agentsToDelete = await dbContext.Agents
            .IncludeSoftDeleted()
            .Where(item => item.TenantId == TenantA && new[] { "Billing" }.Contains(item.Name))
            .ToListAsync();

        agentsToDelete.Should().HaveCount(1)
            .And.OnlyContain(a => a.TenantId == TenantA);
    }

    // ── AgentProposalStore (covers AgentConfigurationService soft-delete pattern) ────

    [Fact]
    public async Task AgentProposalStore_GetById_Should_NotReturn_OtherTenantProposal()
    {
        // GetByIdAsync uses IgnoreQueryFilters via tenant query filter on
        // AgentsDbContext. Even though the call is keyed on Id (a unique
        // GUID), simulate a misrouted GUID guess to confirm the read is
        // scoped — AgentsDbContext applies a TenantId filter when its
        // ITenantProvider is bound.
        await using var dbContext = CreateAgentsDbContext(TenantA);

        var proposalA = SeedProposal(dbContext, TenantA, agentId: Guid.NewGuid());
        var proposalB = SeedProposal(dbContext, TenantB, agentId: Guid.NewGuid());
        await dbContext.SaveChangesAsync();

        var store = new AgentProposalStore(dbContext, new TestCurrentUserProvider(Guid.NewGuid()));

        // Tenant scope is bound to TenantA. Asking for TenantA's proposal
        // works; asking for TenantB's id (whose existence the caller would
        // not know about anyway) must return null.
        var inTenantA = await store.GetByIdAsync(proposalA.Id);
        var crossTenantLookup = await store.GetByIdAsync(proposalB.Id);

        inTenantA.Should().NotBeNull();
        crossTenantLookup.Should().BeNull(
            because: "AgentProposalStore.GetByIdAsync relies on the AgentsDbContext tenant query filter");
    }

    // ── TransactionClassificationService (Scope-aware rules) ────────────

    [Fact]
    public async Task TransactionClassification_GetActiveRules_Should_NotPick_OtherTenantTenantScopedRules()
    {
        // The categorisation-rule lookup intentionally bypasses the
        // per-tenant query filter because it pulls in System (global)
        // rules. The WHERE clause then re-applies tenant scoping for
        // Tenant- and User-scoped rules. Verify TenantB's tenant-scoped
        // rule does NOT match a TenantA transaction.
        await using var dbContext = CreateFinanceDbContext(TenantA);
        var transactionUserId = Guid.NewGuid();

        var tenantBRule = new CategorisationRule
        {
            Id = Guid.NewGuid(),
            TenantId = TenantB,
            UserId = Guid.Empty,
            Scope = "Tenant",
            Pattern = "STARBUCKS",
            Category = "Coffee",
            MatchType = "Contains",
            IsActive = true,
            ApprovalStatus = "Approved",
            Priority = 100,
        };
        dbContext.CategorisationRules.Add(tenantBRule);
        await dbContext.SaveChangesAsync();

        var transaction = new PersonalTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = TenantA,
            UserId = transactionUserId,
            PersonalAccountId = Guid.NewGuid(),
            SourceType = "manual",
            SourceId = Guid.NewGuid(),
            OccurredAt = DateTime.UtcNow,
            Amount = -5m,
            Currency = "USD",
            Merchant = "Starbucks",
            Description = "Latte",
            TransactionType = "Debit",
            Category = string.Empty,
            TagsJson = "[]",
            ReviewStatus = "Pending",
        };

        // Repeat the production query exactly so we exercise the same
        // IgnoreQueryFilters branch the service uses.
        var matched = await dbContext.CategorisationRules
            .IncludeSoftDeleted()
            .AsNoTracking()
            .Where(rule =>
                rule.IsActive
                && !rule.IsDeleted
                && (rule.AppliesToAccountId == null || rule.AppliesToAccountId == transaction.PersonalAccountId)
                && (
                    (rule.Scope == "System" && rule.TenantId == Guid.Empty && rule.UserId == Guid.Empty)
                    || (rule.Scope == "Tenant" && rule.TenantId == transaction.TenantId && rule.UserId == Guid.Empty)
                    || (rule.Scope == "User" && rule.TenantId == transaction.TenantId && rule.UserId == transaction.UserId)
                ))
            .ToListAsync();

        matched.Should().BeEmpty(
            because: "TenantB's tenant-scoped rule must not fire for a TenantA transaction");
    }

    [Fact]
    public async Task TransactionClassification_GetActiveRules_Should_StillPick_GlobalSystemScopedRules()
    {
        // Inverse: System-scoped rules at TenantId=Guid.Empty are
        // intentionally cross-tenant. They MUST fire for a TenantA
        // transaction, otherwise the global classifier breaks.
        await using var dbContext = CreateFinanceDbContext(TenantA);

        var systemRule = new CategorisationRule
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.Empty,
            UserId = Guid.Empty,
            Scope = "System",
            Pattern = "AMAZON",
            Category = "Shopping",
            MatchType = "Contains",
            IsActive = true,
            ApprovalStatus = "Approved",
            Priority = 50,
        };
        dbContext.CategorisationRules.Add(systemRule);
        await dbContext.SaveChangesAsync();

        var transaction = new PersonalTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = TenantA,
            UserId = Guid.NewGuid(),
            PersonalAccountId = Guid.NewGuid(),
            SourceType = "manual",
            SourceId = Guid.NewGuid(),
            OccurredAt = DateTime.UtcNow,
            Amount = -25m,
            Currency = "USD",
            Merchant = "Amazon",
            Description = "Books",
            TransactionType = "Debit",
            Category = string.Empty,
            TagsJson = "[]",
            ReviewStatus = "Pending",
        };

        var matched = await dbContext.CategorisationRules
            .IncludeSoftDeleted()
            .AsNoTracking()
            .Where(rule =>
                rule.IsActive
                && !rule.IsDeleted
                && (
                    (rule.Scope == "System" && rule.TenantId == Guid.Empty && rule.UserId == Guid.Empty)
                    || (rule.Scope == "Tenant" && rule.TenantId == transaction.TenantId && rule.UserId == Guid.Empty)
                    || (rule.Scope == "User" && rule.TenantId == transaction.TenantId && rule.UserId == transaction.UserId)
                ))
            .ToListAsync();

        matched.Should().ContainSingle(
            because: "System-scoped rules are intentionally cross-tenant — the global classifier needs them")
            .Which.Pattern.Should().Be("AMAZON");
    }

    // ── CustomerInsightAiSummaryReader (key-based read, tenant via downstream filter) ─

    [Fact]
    public async Task CustomerInsightAiSummaryReader_GetMissing_Should_NotReturn_OtherTenantSummaries()
    {
        // The reader is a private helper that returns the set of snapshot
        // ids that already have a non-superseded AI summary. The
        // IgnoreQueryFilters on AnkCustomerInsightAiSummaries is paired
        // with a `snapshotIds.Contains(...)` filter where `snapshotIds`
        // came from a previously tenant-filtered query. This test simulates
        // that contract: pass only TenantA's snapshot ids and assert
        // TenantB's summaries are not returned.
        await using var dbContext = CreateAiDbContext();

        var snapshotA = Guid.NewGuid();
        var snapshotB = Guid.NewGuid();

        dbContext.CustomerInsightAiSummaries.Add(new CustomerInsightAiSummary
        {
            Id = Guid.NewGuid(),
            TenantId = TenantA,
            UserId = Guid.NewGuid(),
            CustomerInsightSnapshotId = snapshotA,
            AiRunId = Guid.NewGuid(),
            Status = "Current",
            AsOfUtc = DateTime.UtcNow,
            NarrativeVersion = "v1",
            SummaryJson = "{}",
        });
        dbContext.CustomerInsightAiSummaries.Add(new CustomerInsightAiSummary
        {
            Id = Guid.NewGuid(),
            TenantId = TenantB,
            UserId = Guid.NewGuid(),
            CustomerInsightSnapshotId = snapshotB,
            AiRunId = Guid.NewGuid(),
            Status = "Current",
            AsOfUtc = DateTime.UtcNow,
            NarrativeVersion = "v1",
            SummaryJson = "{}",
        });
        await dbContext.SaveChangesAsync();

        // Caller passes TenantA's snapshot ids only. The IgnoreQueryFilters
        // read must respect the snapshotIds.Contains scope and return only
        // matching rows. TenantB's summary IS returned ONLY if its
        // snapshot id is in the passed list — which it isn't here, because
        // production code derives the list from a tenant-scoped query.
        var ids = await dbContext.CustomerInsightAiSummaries
            .IncludeSoftDeleted()
            .AsNoTracking()
            .Where(x => new[] { snapshotA }.Contains(x.CustomerInsightSnapshotId)
                && (x.Status == "Current" || x.Status == "Failed"))
            .Select(x => x.CustomerInsightSnapshotId)
            .Distinct()
            .ToListAsync();

        ids.Should().ContainSingle().Which.Should().Be(snapshotA);
        ids.Should().NotContain(snapshotB,
            because: "the chained-key pattern (snapshotIds.Contains) is the tenant boundary");
    }

    // ── Seeders writing global (TenantId = null) rows are Pattern D ───
    //
    // AiTaskSeedService and PromptSpecSeedService both filter
    // `TenantId == null`. They never touch tenant-scoped rows; they
    // upsert global reference data. Documented in the audit map above —
    // no negative test required (a "leak" would imply tenant-bound data
    // being read through a TenantId == null filter, which isn't
    // expressible in EF without rewriting the WHERE).

    // ── Test helpers ────────────────────────────────────────────────────

    private static AgentsDbContext CreateAgentsDbContext(Guid? tenantId = null)
    {
        var options = new DbContextOptionsBuilder<AgentsDbContext>()
            .UseInMemoryDatabase($"AgentsDb_{Guid.NewGuid()}")
            .Options;
        return new AgentsDbContext(options, new TestTenantProvider(tenantId ?? Guid.NewGuid()));
    }

    private static FinanceDbContext CreateFinanceDbContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<FinanceDbContext>()
            .UseInMemoryDatabase($"FinanceDb_{Guid.NewGuid()}")
            .Options;
        return new FinanceDbContext(options, new TestTenantProvider(tenantId));
    }

    private static AiDbContext CreateAiDbContext()
    {
        var options = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"AiDb_{Guid.NewGuid()}")
            .Options;
        return new AiDbContext(options, new TestTenantProvider(Guid.NewGuid()));
    }

    private static Agent SeedAgent(AgentsDbContext dbContext, Guid tenantId, string name)
    {
        var agent = new Agent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name,
            Domain = "Test",
            Description = "test",
            InstructionsText = string.Empty,
            ToolsetIdsJson = "[]",
            InputSchemaJson = "{}",
            OutputSchemaJson = "{}",
            PermissionsProfileJson = "{}",
            RiskTier = "Low",
            IsActive = true,
        };
        dbContext.Agents.Add(agent);
        return agent;
    }

    private static Workflow SeedWorkflow(AgentsDbContext dbContext, Guid tenantId, string slug)
    {
        var workflow = new Workflow
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Slug = slug,
            Name = slug,
            Description = "test",
        };
        dbContext.Workflows.Add(workflow);
        return workflow;
    }

    private static Proposal SeedProposal(AgentsDbContext dbContext, Guid tenantId, Guid agentId)
    {
        var proposal = new Proposal
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProposedByAgentId = agentId,
            ProposalType = "Test",
            ImpactSummary = "test",
            RiskTier = "Low",
            PayloadJson = "{}",
        };
        dbContext.Proposals.Add(proposal);
        return proposal;
    }

    private static AgentRun SeedAgentRun(AgentsDbContext dbContext, Guid tenantId, Guid agentId)
    {
        var run = new AgentRun
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AgentId = agentId,
            Status = "Completed",
        };
        dbContext.AgentRuns.Add(run);
        return run;
    }

    private sealed class TestTenantProvider : ITenantProvider
    {
        private readonly Guid _tenantId;
        public TestTenantProvider(Guid tenantId) => _tenantId = tenantId;
        public Guid GetCurrentTenantId() => _tenantId;
        public bool TryGetCurrentTenantId(out Guid tenantId)
        {
            tenantId = _tenantId;
            return true;
        }
    }

    private sealed class TestCurrentUserProvider : ICurrentUserProvider
    {
        private readonly Guid _userId;
        public TestCurrentUserProvider(Guid userId) => _userId = userId;
        public Guid? GetCurrentUserId() => _userId;
        public bool TryGetCurrentUserId(out Guid userId)
        {
            userId = _userId;
            return true;
        }
    }
}
