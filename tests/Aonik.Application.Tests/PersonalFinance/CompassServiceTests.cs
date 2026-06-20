using System.Text.Json;
using Aonik.Finance.Agents;
using Aonik.Finance.Agents.StructuredOutputs;
using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Entities.PersonalFinance;
using Aonik.Finance.Services.PersonalFinance;
using Aonik.PersonalFinance.Persistence;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.PersonalFinance;

// ════════════════════════════════════════════════════════════════════
// AONIK Compass — service tests (Spec 021). Covers RQ1-RQ8, RQ10.
// ════════════════════════════════════════════════════════════════════

public class GoalServiceTests
{
    [Fact]
    public async Task CreateGoalAsync_Should_PersistCompassProgrammeFields_When_Provided()
    {
        // RQ1 — Goal supports Compass programme fields; existing data preserved.
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var db = CompassTestSupport.CreateDbContext(tenantId);
        var service = CompassTestSupport.CreateGoalService(db, tenantId, userId);

        var created = await service.CreateGoalAsync(new CreateGoalRequest(
            Name: "Holiday fund",
            TargetAmount: 2400m,
            Currency: "gbp",
            TargetDate: new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc),
            ProgressAmount: 300m,
            GoalType: "savings",
            Strategy: "Save monthly",
            RiskAppetite: "moderate",
            Priority: 1));

        created.Name.Should().Be("Holiday fund");
        created.Currency.Should().Be("GBP");
        created.Status.Should().Be("Active");
        created.GoalType.Should().Be("savings");
        created.RiskAppetite.Should().Be("moderate");
        created.Priority.Should().Be(1);
        created.ProgressPercent.Should().Be(12.5m);

        var stored = await db.Goals.SingleAsync();
        stored.GoalType.Should().Be("savings");
        stored.Strategy.Should().Be("Save monthly");
    }

    [Fact]
    public async Task UpdateGoalAsync_Should_OnlyChangeProvidedFields()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var db = CompassTestSupport.CreateDbContext(tenantId);
        var service = CompassTestSupport.CreateGoalService(db, tenantId, userId);

        var created = await service.CreateGoalAsync(new CreateGoalRequest("Car", 10000m, "GBP", GoalType: "purchase"));

        var updated = await service.UpdateGoalAsync(created.GoalId, new UpdateGoalRequest(
            ProgressAmount: 2500m,
            RiskAppetite: "aggressive"));

        updated.ProgressAmount.Should().Be(2500m);
        updated.RiskAppetite.Should().Be("aggressive");
        updated.GoalType.Should().Be("purchase", "unspecified fields keep their current values");
        updated.Name.Should().Be("Car");
    }

    [Fact]
    public async Task ListGoalsAsync_Should_ScopeToCurrentUser_And_FilterByStatus()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var otherUser = Guid.NewGuid();
        using var db = CompassTestSupport.CreateDbContext(tenantId);

        var mine = CompassTestSupport.CreateGoalService(db, tenantId, userId);
        var theirs = CompassTestSupport.CreateGoalService(db, tenantId, otherUser);

        await mine.CreateGoalAsync(new CreateGoalRequest("Active goal", 100m, "GBP"));
        var toComplete = await mine.CreateGoalAsync(new CreateGoalRequest("Done goal", 100m, "GBP"));
        await mine.UpdateGoalAsync(toComplete.GoalId, new UpdateGoalRequest(Status: "Completed"));
        await theirs.CreateGoalAsync(new CreateGoalRequest("Not mine", 100m, "GBP"));

        var active = await mine.ListGoalsAsync("Active");
        active.Should().ContainSingle().Which.Name.Should().Be("Active goal");

        var all = await mine.ListGoalsAsync();
        all.Should().HaveCount(2, "the other user's goal is not visible");
    }

    [Fact]
    public async Task CreateGoalAsync_Should_Throw_When_TargetAmountNotPositive()
    {
        var tenantId = Guid.NewGuid();
        using var db = CompassTestSupport.CreateDbContext(tenantId);
        var service = CompassTestSupport.CreateGoalService(db, tenantId, Guid.NewGuid());

        var act = () => service.CreateGoalAsync(new CreateGoalRequest("Bad", 0m, "GBP"));

        await act.Should().ThrowAsync<ArgumentException>();
    }
}

public class CompassGuidanceServiceTests
{
    [Fact]
    public async Task GetSafeToSpendAsync_Should_SubtractObligationsAndPlanCommitments_When_SingleCurrency()
    {
        // RQ5 — deterministic safe-to-spend = liquid - obligations - plan commitments.
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var db = CompassTestSupport.CreateDbContext(tenantId);

        db.PersonalAccounts.Add(new PersonalAccount
        {
            TenantId = tenantId, UserId = userId, Name = "Main",
            AccountType = "Checking", CurrentBalance = 1000m, Currency = "GBP",
        });
        db.Bills.Add(new Bill
        {
            TenantId = tenantId, UserId = userId, Payee = "Rent", Frequency = "Monthly",
            NextDueDate = DateTime.UtcNow.Date.AddDays(5), ExpectedAmount = 400m, Currency = "GBP", Status = "Active",
        });
        // An active plan with a £100/month suggested step → a plan commitment.
        db.CompassPlans.Add(new CompassPlan
        {
            TenantId = tenantId, UserId = userId, GoalId = Guid.NewGuid(), Version = 1,
            Status = "Active", PlanJson = PlanJsonWithStep(100m, "GBP"),
            HorizonStartUtc = DateTime.UtcNow, HorizonEndUtc = DateTime.UtcNow.AddDays(90),
        });
        await db.SaveChangesAsync();

        var service = CreateGuidanceService(db, tenantId, userId, new StubSnapshotReader(StubSnapshotReader.SampleCurrent(userId)));

        var result = await service.GetSafeToSpendAsync(DateTime.UtcNow);

        result.IsPartial.Should().BeFalse();
        result.Currency.Should().Be("GBP");
        result.LiquidAssets.Should().Be(1000m);
        result.ProtectedObligations.Should().Be(400m);
        result.PlanCommitments.Should().Be(100m);
        result.SafeToSpend.Should().Be(500m);
        result.Factors.Should().Contain(f => f.Kind == "PlanCommitment" && f.Amount == 100m);
    }

    [Fact]
    public async Task GetSafeToSpendAsync_Should_ReturnPartialWithWarning_When_MixedCurrency()
    {
        // RQ5 / RQ10 — mixed-currency users get warning-based partial guidance, not a blend.
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var db = CompassTestSupport.CreateDbContext(tenantId);

        db.PersonalAccounts.AddRange(
            new PersonalAccount { TenantId = tenantId, UserId = userId, Name = "GBP", AccountType = "Checking", CurrentBalance = 500m, Currency = "GBP" },
            new PersonalAccount { TenantId = tenantId, UserId = userId, Name = "NGN", AccountType = "Checking", CurrentBalance = 200000m, Currency = "NGN" });
        await db.SaveChangesAsync();

        var service = CreateGuidanceService(db, tenantId, userId, new StubSnapshotReader(StubSnapshotReader.SampleCurrent(userId)));

        var result = await service.GetSafeToSpendAsync(DateTime.UtcNow);

        result.IsPartial.Should().BeTrue();
        result.SafeToSpend.Should().Be(0m, "Compass must not blend currencies in V1");
        result.Warnings.Should().Contain(w => w.Contains("multiple currencies"));
    }

    [Fact]
    public async Task GetSafeToSpendAsync_Should_GenerateSnapshotOnDemand_When_NoneExists()
    {
        // RQ10 / DEC9 — missing snapshot triggers on-demand generation.
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var db = CompassTestSupport.CreateDbContext(tenantId);

        db.PersonalAccounts.Add(new PersonalAccount
        {
            TenantId = tenantId, UserId = userId, Name = "Main",
            AccountType = "Checking", CurrentBalance = 800m, Currency = "GBP",
        });
        await db.SaveChangesAsync();

        var snapshotService = new StubSnapshotService(userId);
        var service = CreateGuidanceService(db, tenantId, userId, new StubSnapshotReader(current: null), snapshotService);

        var result = await service.GetSafeToSpendAsync(DateTime.UtcNow);

        snapshotService.GenerateCalled.Should().BeTrue("no current snapshot existed, so one is generated on demand");
        result.IsPartial.Should().BeFalse();
        result.SafeToSpend.Should().Be(800m);
    }

    [Fact]
    public async Task GetSafeToSpendAsync_Should_ReturnPartialWithWarning_When_SnapshotGenerationFailsAndNoAccounts()
    {
        // RQ10 — insufficient data after fallback returns partial guidance with warnings.
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var db = CompassTestSupport.CreateDbContext(tenantId);

        var snapshotService = new StubSnapshotService(userId, @throw: true);
        var service = CreateGuidanceService(db, tenantId, userId, new StubSnapshotReader(current: null), snapshotService);

        var result = await service.GetSafeToSpendAsync(DateTime.UtcNow);

        result.IsPartial.Should().BeTrue();
        result.Warnings.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateAndListCompassProposal_Should_ReuseProposalWithLinkage_And_ScopeToCurrentUser()
    {
        // RQ7 — Compass recommendations reuse Proposal with goal/plan/user linkage;
        // current-user retrieval filters by linkage (no free-text scan).
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var otherUser = Guid.NewGuid();
        using var db = CompassTestSupport.CreateDbContext(tenantId);

        var goalService = CompassTestSupport.CreateGoalService(db, tenantId, userId);
        var goal = await goalService.CreateGoalAsync(new CreateGoalRequest("Emergency fund", 3000m, "GBP"));

        var store = new FakeAgentProposalStore();
        var service = CreateGuidanceService(db, tenantId, userId, new StubSnapshotReader(), proposalStore: store);

        var planId = Guid.NewGuid();
        var created = await service.CreateCompassProposalAsync(new CreateCompassProposalRequest(
            GoalId: goal.GoalId,
            ActionType: "savings_transfer",
            Amount: 150m,
            Currency: "GBP",
            Rationale: "Build your buffer",
            RiskTier: "low",
            PlanId: planId));

        created.ActionType.Should().Be("savings_transfer");
        store.Created.Should().ContainSingle();
        var stored = store.Created[0];
        stored.ProposalType.Should().Be(CompassGuidanceService.CompassProposalType);
        stored.PayloadJson.Should().Contain(goal.GoalId.ToString());
        stored.PayloadJson.Should().Contain(planId.ToString());
        stored.PayloadJson.Should().Contain(userId.ToString());

        // Same store, a proposal belonging to a different user must not surface.
        store.Created.Add(new AgentProposalCreateRequest(
            Id: Guid.NewGuid(), TenantId: tenantId, ProposalType: CompassGuidanceService.CompassProposalType,
            ProposedByAgentId: Guid.Empty, AiRunId: null, ImpactSummary: "x", RiskTier: "low",
            PayloadJson: JsonSerializer.Serialize(new { userId = otherUser, goalId = Guid.NewGuid(), actionType = "x", amount = 1m, currency = "GBP", rationale = "" })));

        var mine = await service.ListCompassProposalsAsync();
        mine.Should().ContainSingle("only the current user's Compass proposals are returned");
        mine[0].GoalId.Should().Be(goal.GoalId);
    }

    private static CompassGuidanceService CreateGuidanceService(
        PersonalFinanceDbContext db,
        Guid tenantId,
        Guid userId,
        StubSnapshotReader reader,
        StubSnapshotService? snapshotService = null,
        FakeAgentProposalStore? proposalStore = null)
    {
        var tenantProvider = new TestTenantProvider(tenantId);
        var userProvider = new TestCurrentUserProvider(userId);
        var goalService = new GoalService(db, tenantProvider, userProvider);
        return new CompassGuidanceService(
            db,
            reader,
            snapshotService ?? new StubSnapshotService(userId),
            goalService,
            proposalStore ?? new FakeAgentProposalStore(),
            tenantProvider,
            userProvider);
    }

    internal static string PlanJsonWithStep(decimal amount, string currency)
    {
        var plan = new CompassPlanResult(
            SchemaVersion: CompassPlannerStructuredOutputContract.SchemaVersion,
            Summary: "x",
            Steps: new[] { new CompassPlanStep("Save", "r", amount, currency, null) },
            Confidence: 0.8m,
            ReasonCodes: Array.Empty<string>(),
            Entities: Array.Empty<CompassPlanEntity>(),
            Warnings: Array.Empty<string>());
        return JsonSerializer.Serialize(plan, CompassPlannerStructuredOutputContract.SerializerOptions);
    }
}

public class CompassPlanServiceTests
{
    [Fact]
    public async Task GeneratePlanAsync_Should_CreateVersionedPlan_SupersedePrior_And_RecordAiRun()
    {
        // RQ3 / RQ8 — plan created, versioned, supersedes prior, AiRun recorded.
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var db = CompassTestSupport.CreateDbContext(tenantId);

        var goalService = CompassTestSupport.CreateGoalService(db, tenantId, userId);
        var goal = await goalService.CreateGoalAsync(new CreateGoalRequest("Holiday", 2000m, "GBP", GoalType: "savings"));

        var aiRunWriter = new FakeAiRunWriter();
        var planService = CreatePlanService(db, tenantId, userId, new StubCompassPlanGenerator(), aiRunWriter);

        var first = await planService.GeneratePlanAsync(goal.GoalId);
        first.Version.Should().Be(1);
        first.Status.Should().Be("Active");
        first.AiRunId.Should().NotBeNull();
        aiRunWriter.OutcomeFor(first.AiRunId!.Value).Should().Be("Completed");

        var second = await planService.GeneratePlanAsync(goal.GoalId);
        second.Version.Should().Be(2);

        var history = await planService.GetPlanHistoryAsync(goal.GoalId);
        history.Should().HaveCount(2);
        history.Single(p => p.Version == 1).Status.Should().Be("Superseded");
        history.Single(p => p.Version == 2).Status.Should().Be("Active");

        // The goal points at the new active plan.
        var refreshedGoal = await goalService.GetGoalAsync(goal.GoalId);
        refreshedGoal!.ActivePlanId.Should().Be(second.PlanId);

        var current = await planService.GetCurrentPlanAsync(goal.GoalId);
        current!.PlanId.Should().Be(second.PlanId);
    }

    [Fact]
    public async Task GeneratePlanAsync_Should_MarkAiRunFailed_When_PlannerThrows()
    {
        // RQ8 — failed runs are marked failed when planning errors occur.
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var db = CompassTestSupport.CreateDbContext(tenantId);

        var goalService = CompassTestSupport.CreateGoalService(db, tenantId, userId);
        var goal = await goalService.CreateGoalAsync(new CreateGoalRequest("Holiday", 2000m, "GBP"));

        var aiRunWriter = new FakeAiRunWriter();
        var planService = CreatePlanService(db, tenantId, userId, new StubCompassPlanGenerator(@throw: true), aiRunWriter);

        var act = () => planService.GeneratePlanAsync(goal.GoalId);

        await act.Should().ThrowAsync<InvalidOperationException>();
        aiRunWriter.Runs.Should().ContainSingle();
        aiRunWriter.Runs[0].Outcome.Should().Be("Failed");
        (await db.CompassPlans.CountAsync()).Should().Be(0, "no plan is persisted when generation fails");
    }

    [Fact]
    public async Task SupersedePlanAsync_Should_DeactivatePlan_And_ClearGoalPointer()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var db = CompassTestSupport.CreateDbContext(tenantId);

        var goalService = CompassTestSupport.CreateGoalService(db, tenantId, userId);
        var goal = await goalService.CreateGoalAsync(new CreateGoalRequest("Holiday", 2000m, "GBP"));

        var planService = CreatePlanService(db, tenantId, userId, new StubCompassPlanGenerator(), new FakeAiRunWriter());
        var plan = await planService.GeneratePlanAsync(goal.GoalId);

        var superseded = await planService.SupersedePlanAsync(plan.PlanId);

        superseded.Status.Should().Be("Superseded");
        (await goalService.GetGoalAsync(goal.GoalId))!.ActivePlanId.Should().BeNull();
        (await planService.GetCurrentPlanAsync(goal.GoalId)).Should().BeNull();
    }

    [Fact]
    public async Task GeneratePlanAsync_Should_PassDeterministicSafeToSpendIntoPlannerContext()
    {
        // The LLM never computes the number — it is handed the deterministic figure.
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var db = CompassTestSupport.CreateDbContext(tenantId);

        db.PersonalAccounts.Add(new PersonalAccount
        {
            TenantId = tenantId, UserId = userId, Name = "Main",
            AccountType = "Checking", CurrentBalance = 1200m, Currency = "GBP",
        });
        await db.SaveChangesAsync();

        var goalService = CompassTestSupport.CreateGoalService(db, tenantId, userId);
        var goal = await goalService.CreateGoalAsync(new CreateGoalRequest("Holiday", 2000m, "GBP"));

        var generator = new StubCompassPlanGenerator();
        var planService = CreatePlanService(db, tenantId, userId, generator, new FakeAiRunWriter());

        await planService.GeneratePlanAsync(goal.GoalId);

        generator.LastRequest.Should().NotBeNull();
        generator.LastRequest!.Context.SafeToSpend.Should().Be(1200m);
        generator.LastRequest.Context.OperatingCurrency.Should().Be("GBP");
    }

    private static CompassPlanService CreatePlanService(
        PersonalFinanceDbContext db,
        Guid tenantId,
        Guid userId,
        StubCompassPlanGenerator generator,
        FakeAiRunWriter aiRunWriter)
    {
        var tenantProvider = new TestTenantProvider(tenantId);
        var userProvider = new TestCurrentUserProvider(userId);
        var goalService = new GoalService(db, tenantProvider, userProvider);
        var reader = new StubSnapshotReader(StubSnapshotReader.SampleCurrent(userId));
        var guidance = new CompassGuidanceService(
            db, reader, new StubSnapshotService(userId), goalService,
            new FakeAgentProposalStore(), tenantProvider, userProvider);

        return new CompassPlanService(
            db, goalService, guidance, generator, reader, aiRunWriter, tenantProvider, userProvider);
    }
}

public class CompassPlannerAgentTests
{
    [Fact]
    public void Descriptor_Should_BeSubAgentWithOutputSchema()
    {
        // RQ4 — registered as a SubAgent with OutputSchemaJson present.
        IDomainAgentDescriptor descriptor = new CompassPlannerAgentDescriptor();

        descriptor.Name.Should().Be("pf-compass-planner");
        descriptor.AgentType.Should().Be(AgentType.SubAgent);
        descriptor.OutputSchemaJson.Should().NotBeNullOrWhiteSpace();
        descriptor.OutputSchemaJson.Should().Contain("pf_compass_plan.v1");
        descriptor.GetToolNames(serviceProvider: null!).Should().BeEmpty(
            "the planner reasons over its request payload and exposes no host tools");
    }

    [Fact]
    public void CompassPlanResult_Should_ParseFromSchemaConformantFixture()
    {
        // RQ4 — typed output can be parsed from fixture data.
        const string fixture = """
        {
          "schemaVersion": "pf_compass_plan.v1",
          "summary": "Save £100 a month toward your holiday.",
          "steps": [
            { "title": "Standing order", "rationale": "Automate it", "suggestedAmount": 100.0, "currency": "GBP", "targetDate": null }
          ],
          "confidence": 0.82,
          "reasonCodes": ["sized_to_safe_to_spend"],
          "entities": [ { "ref": "goal:123", "label": "Holiday" } ],
          "warnings": []
        }
        """;

        var parsed = JsonSerializer.Deserialize<CompassPlanResult>(
            fixture, CompassPlannerStructuredOutputContract.SerializerOptions);

        parsed.Should().NotBeNull();
        parsed!.SchemaVersion.Should().Be("pf_compass_plan.v1");
        parsed.Steps.Should().ContainSingle();
        parsed.Steps[0].SuggestedAmount.Should().Be(100.0m);
        parsed.Confidence.Should().Be(0.82m);
        parsed.Entities[0].Label.Should().Be("Holiday");
    }
}
