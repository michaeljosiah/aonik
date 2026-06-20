using Aonik.Finance.Agents.StructuredOutputs;
using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using Aonik.Finance.Services.PersonalFinance;
using Aonik.PersonalFinance.Persistence;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Ai;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.PersonalFinance;

/// <summary>
/// Shared fakes + factory helpers for the AONIK Compass service tests (Spec 021).
/// Kept deliberately small — stateful fakes (not strict mocks) so tests assert
/// observable behaviour.
/// </summary>
internal static class CompassTestSupport
{
    public static PersonalFinanceDbContext CreateDbContext(Guid tenantId) =>
        new(new DbContextOptionsBuilder<PersonalFinanceDbContext>()
                .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
                .Options,
            new TestTenantProvider(tenantId));

    public static GoalService CreateGoalService(PersonalFinanceDbContext db, Guid tenantId, Guid userId) =>
        new(db, new TestTenantProvider(tenantId), new TestCurrentUserProvider(userId));
}

/// <summary>In-memory <see cref="IAgentProposalStore"/> capturing created proposals.</summary>
internal sealed class FakeAgentProposalStore : IAgentProposalStore
{
    public List<AgentProposalCreateRequest> Created { get; } = new();

    public Task CreateManyAsync(IReadOnlyList<AgentProposalCreateRequest> requests, CancellationToken cancellationToken = default)
    {
        Created.AddRange(requests);
        return Task.CompletedTask;
    }

    public Task<AgentProposalDetail?> GetByIdAsync(Guid proposalId, CancellationToken cancellationToken = default)
    {
        var match = Created.FirstOrDefault(r => r.Id == proposalId);
        return Task.FromResult(match is null
            ? null
            : new AgentProposalDetail(match.Id, match.TenantId, match.ProposalType, "Proposed", match.PayloadJson, match.ImpactSummary));
    }

    public Task<IReadOnlyList<AgentProposalDetail>> ListProposedAsync(string? proposalType, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<AgentProposalDetail> result = Created
            .Where(r => proposalType is null || r.ProposalType == proposalType)
            .Select(r => new AgentProposalDetail(r.Id, r.TenantId, r.ProposalType, "Proposed", r.PayloadJson, r.ImpactSummary))
            .ToList();
        return Task.FromResult(result);
    }

    public Task ApproveAsync(Guid proposalId, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task RejectAsync(Guid proposalId, string? reason, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

/// <summary>In-memory <see cref="IAiRunWriter"/> recording run lifecycle for assertions.</summary>
internal sealed class FakeAiRunWriter : IAiRunWriter
{
    public List<(Guid Id, string UseCase, string Outcome)> Runs { get; } = new();

    public Task<Guid> StartRunAsync(string useCase, string inputRefsJson, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        Runs.Add((id, useCase, "Started"));
        return Task.FromResult(id);
    }

    public Task MarkRunCompletedAsync(Guid aiRunId, string? outputRef = null, CancellationToken cancellationToken = default)
    {
        Update(aiRunId, "Completed");
        return Task.CompletedTask;
    }

    public Task MarkRunCompletedWithMetricsAsync(Guid aiRunId, int tokensUsed, int latencyMs, decimal costEstimate, string? outputRef = null, CancellationToken cancellationToken = default)
    {
        Update(aiRunId, "Completed");
        return Task.CompletedTask;
    }

    public Task MarkRunFailedAsync(Guid aiRunId, string failureReason, CancellationToken cancellationToken = default)
    {
        Update(aiRunId, "Failed");
        return Task.CompletedTask;
    }

    public Task<Guid> SaveRunAsync(string useCase, string inputRefsJson, string outcome, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        Runs.Add((id, useCase, outcome));
        return Task.FromResult(id);
    }

    public string? OutcomeFor(Guid id) => Runs.Where(r => r.Id == id).Select(r => r.Outcome).LastOrDefault();

    private void Update(Guid id, string outcome)
    {
        for (var i = 0; i < Runs.Count; i++)
        {
            if (Runs[i].Id == id)
            {
                Runs[i] = (id, Runs[i].UseCase, outcome);
            }
        }
    }
}

/// <summary>
/// Stub <see cref="ICompassPlanGenerator"/> that returns a canned plan (no LLM).
/// Optionally throws to exercise the failed-run path.
/// </summary>
internal sealed class StubCompassPlanGenerator : ICompassPlanGenerator
{
    private readonly bool _throw;
    public CompassPlannerRequest? LastRequest { get; private set; }

    public StubCompassPlanGenerator(bool @throw = false) => _throw = @throw;

    public Task<CompassPlannerAgentToolResponse> GenerateAsync(CompassPlannerRequest request, CancellationToken cancellationToken = default)
    {
        LastRequest = request;
        if (_throw)
        {
            throw new InvalidOperationException("planner boom");
        }

        var plan = new CompassPlanResult(
            SchemaVersion: CompassPlannerStructuredOutputContract.SchemaVersion,
            Summary: "Set aside a little each month toward your goal.",
            Steps: new[]
            {
                new CompassPlanStep("Set up a standing order", "Automate contributions.", 50m, request.Currency, null),
            },
            Confidence: 0.8m,
            ReasonCodes: new[] { "sized_to_safe_to_spend" },
            Entities: new[] { new CompassPlanEntity($"goal:{request.GoalId}", request.GoalName) },
            Warnings: Array.Empty<string>());

        var json = System.Text.Json.JsonSerializer.Serialize(plan, CompassPlannerStructuredOutputContract.SerializerOptions);
        return Task.FromResult(new CompassPlannerAgentToolResponse(plan, json));
    }
}

/// <summary>
/// Stub snapshot reader. Returns a configurable "current" snapshot (or null to
/// exercise the missing-snapshot path).
/// </summary>
internal sealed class StubSnapshotReader : ICustomerInsightSnapshotReader
{
    private readonly CustomerInsightSnapshotResponse? _current;

    public StubSnapshotReader(CustomerInsightSnapshotResponse? current = null) => _current = current;

    public Task<CustomerInsightSnapshotResponse?> GetCurrentSnapshotAsync(Guid userId, CancellationToken cancellationToken = default)
        => Task.FromResult(_current);

    public Task<CustomerInsightSnapshotResponse?> GetSnapshotAsync(Guid snapshotId, CancellationToken cancellationToken = default)
        => Task.FromResult<CustomerInsightSnapshotResponse?>(null);

    public Task<IReadOnlyList<CustomerInsightSnapshotHistoryItemResponse>> GetSnapshotHistoryAsync(Guid userId, int take = 20, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<CustomerInsightSnapshotHistoryItemResponse>>(Array.Empty<CustomerInsightSnapshotHistoryItemResponse>());

    public static CustomerInsightSnapshotResponse SampleCurrent(Guid userId) => new(
        Id: Guid.NewGuid(),
        UserId: userId,
        Status: "Current",
        AsOfUtc: DateTime.UtcNow,
        WindowStartUtc: DateTime.UtcNow.AddDays(-30),
        WindowEndUtc: DateTime.UtcNow,
        Version: 1,
        SourceHash: "hash",
        GeneratedBy: "test",
        GenerationDurationMs: 5,
        FailureReason: null,
        SupersededById: null,
        CreatedAt: DateTime.UtcNow,
        UpdatedAt: null,
        Snapshot: null);
}

/// <summary>
/// Stub snapshot service. Either returns an on-demand-generated snapshot or
/// throws (to exercise the "snapshot could not be generated" warning path).
/// Tracks whether on-demand generation was invoked.
/// </summary>
internal sealed class StubSnapshotService : ICustomerInsightSnapshotService
{
    private readonly Guid _userId;
    private readonly bool _throw;
    public bool GenerateCalled { get; private set; }

    public StubSnapshotService(Guid userId, bool @throw = false)
    {
        _userId = userId;
        _throw = @throw;
    }

    public Task<CustomerInsightSnapshotResponse> GenerateCurrentSnapshotAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        GenerateCalled = true;
        if (_throw)
        {
            throw new InvalidOperationException("snapshot generation failed");
        }

        return Task.FromResult(StubSnapshotReader.SampleCurrent(_userId));
    }
}
