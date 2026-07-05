using Aonik.Ai.Entities;
using Aonik.Ai.Observability;
using Aonik.Ai.Persistence;
using Aonik.Ai.Services;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ZiggyCreatures.Caching.Fusion;

namespace Aonik.Application.Tests.Ai;

/// <summary>
/// Unit tests for <see cref="AiRunWriter"/> covering the non-kill-switch
/// surface area (Save / MarkCompleted / MarkFailed / metric capture).
/// Uses xUnit + Moq + FluentAssertions per the project's standard
/// testing stack.
/// </summary>
/// <remarks>
/// The kill-switch enforcement path is covered separately in
/// <see cref="AiRunWriterKillSwitchTests"/>; this file focuses on the
/// happy paths and outcome transitions on top of that.
/// </remarks>
public class AiRunWriterTests
{
    private static readonly Guid TenantId = Guid.Parse("aa000000-0000-0000-0000-000000000001");
    private static readonly Guid CallingUserId = Guid.Parse("aa100000-0000-0000-0000-000000000099");

    private readonly Mock<ITenantProvider> _tenantProvider;
    private readonly Mock<ICurrentUserProvider> _currentUserProvider;
    private readonly IFusionCache _cache;

    public AiRunWriterTests()
    {
        // Loose mode (default): the AonikDbContextBase tenant filter
        // calls TryGetCurrentTenantId on its own without going through
        // the service code under test, so we don't want to fail tests on
        // those incidental invocations. We Setup the calls we ARE about
        // to verify and let the rest fall through to defaults.
        _tenantProvider = new Mock<ITenantProvider>();
        _tenantProvider.Setup(x => x.GetCurrentTenantId()).Returns(TenantId);
        _tenantProvider
            .Setup(x => x.TryGetCurrentTenantId(out It.Ref<Guid>.IsAny))
            .Callback(new TryGetCurrentTenantIdDelegate((out Guid id) => id = TenantId))
            .Returns(true);

        _currentUserProvider = new Mock<ICurrentUserProvider>();
        _currentUserProvider.Setup(x => x.GetCurrentUserId()).Returns(CallingUserId);

        _cache = CreateFusionCache();
    }

    private delegate void TryGetCurrentTenantIdDelegate(out Guid tenantId);

    [Fact]
    public async Task StartRunAsync_Should_PersistAiRun_With_StartedOutcome_And_Trimmed_UseCase()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var writer = NewWriter(dbContext);

        // Act
        var runId = await writer.StartRunAsync("  invoice.summary  ", "{\"invoiceId\":\"abc\"}");

        // Assert
        runId.Should().NotBeEmpty();

        var stored = await dbContext.AiRuns.FirstOrDefaultAsync(r => r.Id == runId);
        stored.Should().NotBeNull();
        stored!.UseCase.Should().Be("invoice.summary", because: "useCase is trimmed before persisting");
        stored.Outcome.Should().Be("Started");
        stored.TenantId.Should().Be(TenantId);
        stored.UserId.Should().Be(CallingUserId);
        stored.InputRefsJson.Should().Be("{\"invoiceId\":\"abc\"}");

        _tenantProvider.Verify(x => x.GetCurrentTenantId(), Times.Once);
        _currentUserProvider.Verify(x => x.GetCurrentUserId(), Times.Once);
    }

    [Fact]
    public async Task StartRunAsync_Should_DefaultBlankInputRefs_To_EmptyJsonObject()
    {
        await using var dbContext = CreateDbContext();
        var writer = NewWriter(dbContext);

        var runId = await writer.StartRunAsync("noop", "");

        var stored = await dbContext.AiRuns.FirstAsync(r => r.Id == runId);
        stored.InputRefsJson.Should().Be("{}");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task StartRunAsync_Should_Throw_When_UseCaseIsBlank(string? useCase)
    {
        await using var dbContext = CreateDbContext();
        var writer = NewWriter(dbContext);

        var act = async () => await writer.StartRunAsync(useCase!, "{}");

        await act.Should().ThrowAsync<ArgumentException>()
            .Where(e => e.ParamName == "useCase");
    }

    [Fact]
    public async Task SaveRunAsync_With_StartedOutcome_Should_Leave_Run_In_StartedState()
    {
        await using var dbContext = CreateDbContext();
        var writer = NewWriter(dbContext);

        var runId = await writer.SaveRunAsync("noop", "{}", outcome: "Started");

        var stored = await dbContext.AiRuns.FirstAsync(r => r.Id == runId);
        stored.Outcome.Should().Be("Started");
    }

    [Fact]
    public async Task SaveRunAsync_With_BlankOrCompletedOutcome_Should_Mark_Run_Completed()
    {
        await using var dbContext = CreateDbContext();
        var writer = NewWriter(dbContext);

        var blankOutcomeRunId = await writer.SaveRunAsync("noop", "{}", outcome: "");
        var completedOutcomeRunId = await writer.SaveRunAsync("noop", "{}", outcome: "completed");

        var stored = await dbContext.AiRuns.Where(r => r.Id == blankOutcomeRunId || r.Id == completedOutcomeRunId).ToListAsync();
        stored.Should().HaveCount(2);
        stored.Should().OnlyContain(r => r.Outcome == "Completed");
    }

    [Fact]
    public async Task SaveRunAsync_With_CustomOutcome_Should_Persist_Trimmed_Outcome()
    {
        await using var dbContext = CreateDbContext();
        var writer = NewWriter(dbContext);

        var runId = await writer.SaveRunAsync("noop", "{}", outcome: "  PartiallyStreamed  ");

        var stored = await dbContext.AiRuns.FirstAsync(r => r.Id == runId);
        stored.Outcome.Should().Be("PartiallyStreamed");
    }

    [Fact]
    public async Task MarkRunCompletedAsync_Should_FlipOutcome_And_StoreOutputRef()
    {
        await using var dbContext = CreateDbContext();
        var writer = NewWriter(dbContext);
        var runId = await writer.StartRunAsync("noop", "{}");

        await writer.MarkRunCompletedAsync(runId, outputRef: "  blob://result/123  ");

        var stored = await dbContext.AiRuns.FirstAsync(r => r.Id == runId);
        stored.Outcome.Should().Be("Completed");
        stored.OutputRef.Should().Be("blob://result/123",
            because: "OutputRef is trimmed of surrounding whitespace");
    }

    [Fact]
    public async Task MarkRunCompletedAsync_Should_StoreNullOutputRef_When_BlankProvided()
    {
        await using var dbContext = CreateDbContext();
        var writer = NewWriter(dbContext);
        var runId = await writer.StartRunAsync("noop", "{}");

        await writer.MarkRunCompletedAsync(runId, outputRef: "   ");

        var stored = await dbContext.AiRuns.FirstAsync(r => r.Id == runId);
        stored.OutputRef.Should().BeNull();
    }

    [Fact]
    public async Task MarkRunCompletedAsync_Should_Throw_When_RunDoesNotExist()
    {
        await using var dbContext = CreateDbContext();
        var writer = NewWriter(dbContext);

        var act = async () => await writer.MarkRunCompletedAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("AiRun * not found.");
    }

    [Fact]
    public async Task MarkRunFailedAsync_Should_FlipOutcomeToFailed_And_TruncateLongReason_To_200Chars()
    {
        await using var dbContext = CreateDbContext();
        var writer = NewWriter(dbContext);
        var runId = await writer.StartRunAsync("noop", "{}");
        var reason = new string('x', 500);

        await writer.MarkRunFailedAsync(runId, reason);

        var stored = await dbContext.AiRuns.FirstAsync(r => r.Id == runId);
        stored.Outcome.Should().Be("Failed");
        stored.OutputRef.Should().NotBeNull();
        stored.OutputRef!.Length.Should().Be(200,
            because: "the failure reason is capped at 200 chars to keep the AiRun row small");
    }

    [Fact]
    public async Task MarkRunFailedAsync_Should_DefaultBlankReasonTo_UnknownError()
    {
        await using var dbContext = CreateDbContext();
        var writer = NewWriter(dbContext);
        var runId = await writer.StartRunAsync("noop", "{}");

        await writer.MarkRunFailedAsync(runId, "   ");

        var stored = await dbContext.AiRuns.FirstAsync(r => r.Id == runId);
        stored.OutputRef.Should().Be("Unknown error");
    }

    [Fact]
    public async Task MarkRunCompletedWithMetricsAsync_Should_RecordMetrics_And_UseExplicitCost_When_NonZero()
    {
        await using var dbContext = CreateDbContext();
        var writer = NewWriter(dbContext);
        var runId = await writer.StartRunAsync("noop", "{}");

        await writer.MarkRunCompletedWithMetricsAsync(
            runId,
            tokensUsed: 1234,
            latencyMs: 567,
            costEstimate: 0.5m,
            outputRef: "blob://x");

        var stored = await dbContext.AiRuns.FirstAsync(r => r.Id == runId);
        stored.Outcome.Should().Be("Completed");
        stored.TokensUsed.Should().Be(1234);
        stored.LatencyMs.Should().Be(567);
        stored.CostEstimate.Should().Be(0.5m, because: "explicit cost overrides the model's CostProfileJson lookup");
        stored.OutputRef.Should().Be("blob://x");
    }

    [Fact]
    public async Task MarkRunCompletedWithMetricsAsync_Should_AutoComputeCost_From_ModelCostProfile_When_ZeroExplicitCost()
    {
        await using var dbContext = CreateDbContext();
        var writer = NewWriter(dbContext);
        var runId = await writer.StartRunAsync("noop", "{}");

        // Replace the auto-seeded stub model's CostProfileJson with a real
        // models.dev-shape profile so the auto-compute fallback has
        // something meaningful to multiply against. Format expected by
        // AiCostCalculator is { "input": <usd-per-million>, "output": <usd-per-million> }.
        var run = await dbContext.AiRuns.FirstAsync(r => r.Id == runId);
        var model = await dbContext.AiModels.FirstAsync(m => m.Id == run.AiModelId);
        model.CostProfileJson = "{\"input\":0.5,\"output\":1.5}";
        await dbContext.SaveChangesAsync();

        await writer.MarkRunCompletedWithMetricsAsync(
            runId,
            tokensUsed: 2000,
            latencyMs: 100,
            costEstimate: 0m);

        var stored = await dbContext.AiRuns.FirstAsync(r => r.Id == runId);
        stored.CostEstimate.Should()
            .BeGreaterThan(0m, because: "cost is auto-computed from the model's CostProfileJson when caller passes 0");
    }

    [Fact]
    public async Task MarkRunCompletedWithMetricsAsync_Should_Throw_When_RunDoesNotExist()
    {
        await using var dbContext = CreateDbContext();
        var writer = NewWriter(dbContext);

        var act = async () => await writer.MarkRunCompletedWithMetricsAsync(
            Guid.NewGuid(),
            tokensUsed: 0,
            latencyMs: 0,
            costEstimate: 0m);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("AiRun * not found.");
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    [Fact]
    public async Task MarkRunCompletedWithMetricsAsync_Should_UseDatabaseFallback_When_RunStartedInDifferentScope()
    {
        // Two contexts sharing one InMemory database: start the run in one, complete it in a fresh
        // one whose change tracker is empty — proving LoadRunAsync's DB fallback FINDS the run
        // (not just the not-found/throw path).
        var databaseName = $"AiRunWriter_Shared_{Guid.NewGuid()}";
        Guid runId;
        await using (var startContext = CreateDbContext(databaseName))
        {
            runId = await NewWriter(startContext).StartRunAsync("noop", "{}");
        }

        await using (var completeContext = CreateDbContext(databaseName))
        {
            completeContext.AiRuns.Local.Should().BeEmpty(
                "the completion context tracks nothing, so the fallback must query the database");

            await NewWriter(completeContext).MarkRunCompletedWithMetricsAsync(
                runId, tokensUsed: 55, latencyMs: 10, costEstimate: 0m);
        }

        await using var verifyContext = CreateDbContext(databaseName);
        var stored = await verifyContext.AiRuns.FirstAsync(r => r.Id == runId);
        stored.Outcome.Should().Be("Completed");
        stored.TokensUsed.Should().Be(55);
    }

    private AiRunWriter NewWriter(AiDbContext dbContext)
        => new(dbContext, _tenantProvider.Object, _currentUserProvider.Object, _cache, new AiRunMetrics());

    private AiDbContext CreateDbContext() => CreateDbContext($"AiRunWriter_{Guid.NewGuid()}");

    private AiDbContext CreateDbContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        return new AiDbContext(options, _tenantProvider.Object);
    }

    private static IFusionCache CreateFusionCache()
    {
        var services = new ServiceCollection();
        services.AddFusionCache();
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IFusionCache>();
    }
}
