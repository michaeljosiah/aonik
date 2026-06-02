using System.Linq;
using System.Text.Json;

using Aonik.Agents.Contracts.Models;
using Aonik.Agents.Contracts.Services;
using Aonik.Agents.Entities;
using Aonik.Agents.Services;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Multitenancy;

using FluentAssertions;

namespace Aonik.Application.Tests.Agents.Approval;

/// <summary>
/// Spec 032 §7.5 — the tier router + decision authority (<see cref="ToolApprovalService"/>) the gate
/// decorator delegates to. These tests pin the slice's behaviour end-to-end:
/// <list type="bullet">
///   <item>Low persists an Approved+consumed request and reports
///   <see cref="ToolGateDecision.ApprovedInline"/>; the decorator runs it in-band.</item>
///   <item>Medium creates a Pending request and reports
///   <see cref="ToolGateDecision.PendingApproval"/>; once approved, a resubmit with the <em>same
///   arguments</em> consumes the approval (single-use) and reports ApprovedInline, while changed
///   arguments never match (replay guard).</item>
///   <item>High marshals into exactly one Proposed/High proposal carrying the verbatim payload and
///   reports <see cref="ToolGateDecision.Queued"/>; the fail-closed
///   <see cref="ToolGateDecision.Refused"/> paths never touch the proposal store.</item>
///   <item><see cref="ToolApprovalService.DecideAsync"/> enforces identity, tenant, expiry, and
///   single-use status; a High decision routes through the policy-checked proposal-approval path.</item>
/// </list>
/// </summary>
public class ToolApprovalServiceTests
{
    // ----- Test doubles -----------------------------------------------------------------------

    /// <summary>In-memory <see cref="IToolApprovalRequestStore"/> mirroring the real consumable query.</summary>
    private sealed class InMemoryRequestStore : IToolApprovalRequestStore
    {
        public List<ToolApprovalRequest> Requests { get; } = new();

        public Task CreateAsync(ToolApprovalRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.CompletedTask;
        }

        public Task<ToolApprovalRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Requests.FirstOrDefault(r => r.Id == id));

        public Task<ToolApprovalRequest?> FindConsumableApprovedAsync(
            Guid tenantId, Guid? requestingUserId, string toolName, string argsHash, DateTime nowUtc,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Requests
                .Where(r => r.TenantId == tenantId
                    && r.RequestingUserId == requestingUserId
                    && r.ToolName == toolName
                    && r.ArgsHash == argsHash
                    && r.Status == ToolApprovalRequestStatus.Approved
                    && r.ConsumedAt == null
                    && r.ExpiresAt > nowUtc)
                .OrderBy(r => r.RequestedAt)
                .FirstOrDefault());

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class RecordingProposalStore : IAgentProposalStore
    {
        public List<AgentProposalCreateRequest> Created { get; } = new();

        public Task CreateManyAsync(IReadOnlyList<AgentProposalCreateRequest> requests, CancellationToken cancellationToken = default)
        {
            Created.AddRange(requests);
            return Task.CompletedTask;
        }

        public Task<AgentProposalDetail?> GetByIdAsync(Guid proposalId, CancellationToken cancellationToken = default) =>
            Task.FromResult<AgentProposalDetail?>(null);

        public Task<IReadOnlyList<AgentProposalDetail>> ListProposedAsync(string? proposalType, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AgentProposalDetail>>(Array.Empty<AgentProposalDetail>());

        public Task ApproveAsync(Guid proposalId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RejectAsync(Guid proposalId, string? reason, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    /// <summary>Stub proposal-approval service for the High <c>DecideAsync</c> path.</summary>
    private sealed class StubProposalApprovalService : IProposalApprovalService
    {
        /// <summary>What <see cref="GetByIdAsync"/> and <see cref="ApproveAsync"/> return; null ⇒ "missing".</summary>
        public ProposalDetailResponse? Detail { get; set; }

        /// <summary>If set, <see cref="ApproveAsync"/> throws it (simulates a failed money dispatch).</summary>
        public Exception? ApproveThrows { get; set; }

        public List<Guid> ApprovedProposalIds { get; } = new();

        public Task<ProposalDetailResponse?> GetByIdAsync(Guid proposalId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Detail);

        public Task<ListProposalsResponse> ListPendingAsync(ListProposalsRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ListProposalsResponse(Array.Empty<ProposalListItem>(), 0));

        public Task<ProposalDetailResponse> ApproveAsync(Guid proposalId, CancellationToken cancellationToken = default)
        {
            if (ApproveThrows is not null)
            {
                throw ApproveThrows;
            }

            ApprovedProposalIds.Add(proposalId);
            return Task.FromResult(Detail ?? throw new InvalidOperationException("No proposal detail configured."));
        }

        public Task<ProposalDetailResponse> DismissAsync(Guid proposalId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class StubProposalApprovalPolicy : IProposalApprovalPolicy
    {
        public ApprovalAuthorization Result { get; set; } = ApprovalAuthorization.Allowed;

        public ProposalAuthorizationContext? LastContext { get; private set; }

        public ApprovalActor? LastActor { get; private set; }

        public ApprovalAuthorization Authorize(ApprovalActor actor, ProposalAuthorizationContext context)
        {
            LastActor = actor;
            LastContext = context;
            return Result;
        }
    }

    private sealed class TestTenantProvider(Guid? tenantId) : ITenantProvider
    {
        public Guid? TenantId { get; set; } = tenantId;

        public Guid GetCurrentTenantId() => TenantId ?? throw new InvalidOperationException("no tenant in scope");

        public bool TryGetCurrentTenantId(out Guid id)
        {
            id = TenantId ?? Guid.Empty;
            return TenantId.HasValue;
        }
    }

    private sealed class TestCurrentUserProvider(Guid? userId) : ICurrentUserProvider
    {
        public Guid? UserId { get; set; } = userId;

        public Guid? GetCurrentUserId() => UserId;

        public bool TryGetCurrentUserId(out Guid id)
        {
            id = UserId ?? Guid.Empty;
            return UserId.HasValue;
        }
    }

    private sealed class TestClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; set; } = utcNow;
    }

    /// <summary>Wires the seven dependencies with mutable providers so a single built service can see
    /// different ambient user / clock values across a gate → decide → resubmit sequence.</summary>
    private sealed class Fixture
    {
        public InMemoryRequestStore RequestStore { get; } = new();
        public RecordingProposalStore ProposalStore { get; } = new();
        public StubProposalApprovalService ApprovalService { get; } = new();
        public StubProposalApprovalPolicy Policy { get; } = new();
        public TestTenantProvider Tenant { get; } = new(Guid.NewGuid());
        public TestCurrentUserProvider User { get; } = new(Guid.NewGuid());
        public TestClock Clock { get; } = new(new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc));

        public ToolApprovalService Build() =>
            new(RequestStore, ProposalStore, ApprovalService, Policy, Tenant, User, Clock);
    }

    private static ToolGateContext Low(IDictionary<string, object?>? args = null) =>
        new("pf_set_preference", new ToolApprovalOptions(ToolApprovalTier.Low, "Set a preference"),
            args ?? new Dictionary<string, object?>());

    private static ToolGateContext Medium(IDictionary<string, object?>? args = null) =>
        new("finance_create_invoice", new ToolApprovalOptions(ToolApprovalTier.Medium, "Create an invoice"),
            args ?? new Dictionary<string, object?>());

    private static ToolGateContext High(
        string? proposalType = "Finance.CapturePayment",
        IDictionary<string, object?>? args = null) =>
        new("finance_capture_payment", new ToolApprovalOptions(ToolApprovalTier.High, "Capture a payment", proposalType),
            args ?? new Dictionary<string, object?>());

    private static ProposalDetailResponse ProposalDetail(Guid id, string type = "Finance.CapturePayment", string risk = "High") =>
        new(
            Id: id,
            ProposalType: type,
            ProposedByAgentId: Guid.Empty,
            AgentName: "Test",
            AgentDomain: "Finance",
            AgentIconUrl: null,
            AiRunId: Guid.Empty,
            Summary: "Capture a payment",
            RiskTier: risk,
            Confidence: 1m,
            Status: "Proposed",
            ApprovedByUserId: null,
            ApprovedAt: null,
            PayloadJson: "{}",
            CreatedAt: DateTime.UtcNow);

    // ----- GateAsync: tier routing ------------------------------------------------------------

    [Fact]
    public async Task GateAsync_Should_ApproveInlineAndRecordConsumedRequest_When_TierIsLow()
    {
        var f = new Fixture();
        var service = f.Build();

        var outcome = await service.GateAsync(Low());

        outcome.Decision.Should().Be(ToolGateDecision.ApprovedInline);
        outcome.ProposalId.Should().BeNull();
        outcome.ApprovalRequestId.Should().NotBeNull("Low still records a durable audit row when a tenant is in scope");

        f.ProposalStore.Created.Should().BeEmpty("Low never marshals into a proposal");
        f.RequestStore.Requests.Should().ContainSingle();
        var request = f.RequestStore.Requests[0];
        request.RiskTier.Should().Be("Low");
        request.Status.Should().Be(ToolApprovalRequestStatus.Approved, "Low is auto-approved");
        request.ConsumedAt.Should().NotBeNull("the in-band run consumes it immediately");
    }

    [Fact]
    public async Task GateAsync_Should_ApproveInlineWithoutPersisting_When_LowAndNoTenant()
    {
        var f = new Fixture();
        f.Tenant.TenantId = null;
        var service = f.Build();

        var outcome = await service.GateAsync(Low());

        outcome.Decision.Should().Be(ToolGateDecision.ApprovedInline, "a reversible Low write is not blocked by a missing tenant");
        outcome.ApprovalRequestId.Should().BeNull();
        f.RequestStore.Requests.Should().BeEmpty("no tenant ⇒ no tenant-scoped row can be written");
    }

    [Fact]
    public async Task GateAsync_Should_CreatePendingRequestAndReturnPendingApproval_When_MediumFirstCall()
    {
        var f = new Fixture();
        var service = f.Build();

        var outcome = await service.GateAsync(Medium());

        outcome.Decision.Should().Be(ToolGateDecision.PendingApproval, "Medium requires an explicit confirmation before running");
        outcome.ApprovalRequestId.Should().NotBeNull();

        f.ProposalStore.Created.Should().BeEmpty("Medium is not a money movement, so it is never marshalled into a proposal");
        f.RequestStore.Requests.Should().ContainSingle();
        var request = f.RequestStore.Requests[0];
        request.RiskTier.Should().Be("Medium");
        request.Status.Should().Be(ToolApprovalRequestStatus.Pending);
        request.ExpiresAt.Should().BeAfter(f.Clock.UtcNow, "a Pending request must stay decidable for a window");
    }

    [Fact]
    public async Task GateAsync_Should_RefuseWithoutPersisting_When_MediumAndNoTenant()
    {
        var f = new Fixture();
        f.Tenant.TenantId = null;
        var service = f.Build();

        var outcome = await service.GateAsync(Medium());

        outcome.Decision.Should().Be(ToolGateDecision.Refused, "with no tenant there is no durable row to track the decision, so fail closed");
        f.RequestStore.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task GateAsync_Should_CreateProposedHighProposalQueueAndLinkRequest_When_HighWithProposalTypeAndTenant()
    {
        var f = new Fixture();
        var service = f.Build();

        var paymentIntentId = Guid.NewGuid();
        var arguments = new Dictionary<string, object?> { ["paymentIntentId"] = paymentIntentId };

        var outcome = await service.GateAsync(High(args: arguments));

        outcome.Decision.Should().Be(ToolGateDecision.Queued);
        outcome.ProposalId.Should().NotBeNull();
        outcome.ApprovalRequestId.Should().NotBeNull();

        f.ProposalStore.Created.Should().ContainSingle("a High tool marshals into exactly one durable proposal");
        var created = f.ProposalStore.Created[0];
        created.Id.Should().Be(outcome.ProposalId!.Value);
        created.TenantId.Should().Be(f.Tenant.TenantId!.Value);
        created.ProposalType.Should().Be("Finance.CapturePayment");
        created.RiskTier.Should().Be("High");
        created.ImpactSummary.Should().Be("Capture a payment");

        // Payload round-trips the verbatim model arguments so the IProposalHandler can read them back.
        using var doc = JsonDocument.Parse(created.PayloadJson);
        doc.RootElement.GetProperty("paymentIntentId").GetGuid().Should().Be(paymentIntentId);

        // A durable correlation row is created and linked to the proposal, left Pending (the proposal
        // pipeline executes High; the request is never consumed for an inline re-run).
        f.RequestStore.Requests.Should().ContainSingle();
        var request = f.RequestStore.Requests[0];
        request.ProposalId.Should().Be(outcome.ProposalId!.Value);
        request.Status.Should().Be(ToolApprovalRequestStatus.Pending);
    }

    [Fact]
    public async Task GateAsync_Should_RefuseWithoutTouchingStores_When_HighHasNoProposalType()
    {
        var f = new Fixture();
        var service = f.Build();

        var outcome = await service.GateAsync(High(proposalType: null));

        outcome.Decision.Should().Be(ToolGateDecision.Refused);
        outcome.Reason.Should().NotBeNullOrWhiteSpace();
        f.ProposalStore.Created.Should().BeEmpty("a misconfigured High tool must never create a proposal");
        f.RequestStore.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task GateAsync_Should_RefuseWithoutTouchingStores_When_HighAndNoTenant()
    {
        var f = new Fixture();
        f.Tenant.TenantId = null;
        var service = f.Build();

        var outcome = await service.GateAsync(High());

        outcome.Decision.Should().Be(ToolGateDecision.Refused);
        f.ProposalStore.Created.Should().BeEmpty();
        f.RequestStore.Requests.Should().BeEmpty();
    }

    // ----- Medium multi-turn: consume once, replay guard, rejection -------------------------------

    [Fact]
    public async Task Medium_Should_ConsumeApprovalAndRunOnce_When_ResubmittedWithSameArgsAfterApprove()
    {
        var f = new Fixture();
        var service = f.Build();
        var args = new Dictionary<string, object?> { ["customerId"] = "c-1", ["amount"] = 100 };

        // 1) First call → Pending.
        var first = await service.GateAsync(Medium(args));
        first.Decision.Should().Be(ToolGateDecision.PendingApproval);
        var requestId = first.ApprovalRequestId!.Value;

        // 2) The user approves.
        var decision = await service.DecideAsync(requestId, new ToolApprovalDecisionInput(ToolApprovalDecisionType.Approve));
        decision.Outcome.Should().Be(ToolApprovalDecisionOutcome.Approved);

        // 3) Agent re-invokes the same call → the approval is consumed and the inner tool runs once.
        var second = await service.GateAsync(Medium(args));
        second.Decision.Should().Be(ToolGateDecision.ApprovedInline);
        second.ApprovalRequestId.Should().Be(requestId, "the matching approved request was consumed");

        // 4) A further re-invoke finds nothing consumable (single-use) → a fresh Pending request.
        var third = await service.GateAsync(Medium(args));
        third.Decision.Should().Be(ToolGateDecision.PendingApproval);
        third.ApprovalRequestId.Should().NotBe(requestId, "an approval is single-use and cannot drive a second run");
    }

    [Fact]
    public async Task Medium_Should_NotConsumeApproval_When_ResubmittedWithChangedArgs()
    {
        var f = new Fixture();
        var service = f.Build();
        var original = new Dictionary<string, object?> { ["amount"] = 100 };
        var tampered = new Dictionary<string, object?> { ["amount"] = 999 };

        var first = await service.GateAsync(Medium(original));
        var requestId = first.ApprovalRequestId!.Value;
        await service.DecideAsync(requestId, new ToolApprovalDecisionInput(ToolApprovalDecisionType.Approve));

        // Resubmit with DIFFERENT arguments — the args-hash no longer matches the approved request.
        var resubmit = await service.GateAsync(Medium(tampered));

        resubmit.Decision.Should().Be(ToolGateDecision.PendingApproval, "a decision never authorises a call with changed arguments");
        resubmit.ApprovalRequestId.Should().NotBe(requestId);

        var approved = f.RequestStore.Requests.Single(r => r.Id == requestId);
        approved.Status.Should().Be(ToolApprovalRequestStatus.Approved);
        approved.ConsumedAt.Should().BeNull("the tampered resubmit must not consume the original approval");
    }

    [Fact]
    public async Task Medium_Should_StayBlocked_When_Rejected()
    {
        var f = new Fixture();
        var service = f.Build();
        var args = new Dictionary<string, object?> { ["amount"] = 100 };

        var first = await service.GateAsync(Medium(args));
        var requestId = first.ApprovalRequestId!.Value;

        var decision = await service.DecideAsync(requestId, new ToolApprovalDecisionInput(ToolApprovalDecisionType.Reject, "not now"));
        decision.Outcome.Should().Be(ToolApprovalDecisionOutcome.Rejected);

        var resubmit = await service.GateAsync(Medium(args));
        resubmit.Decision.Should().Be(ToolGateDecision.PendingApproval, "a rejected request can never be consumed for an inline run");
    }

    // ----- DecideAsync: validation ------------------------------------------------------------

    [Fact]
    public async Task DecideAsync_Should_ReturnForbidden_When_NoAuthenticatedUser()
    {
        var f = new Fixture();
        var service = f.Build();
        var first = await service.GateAsync(Medium());

        f.User.UserId = null; // the decision arrives with no authenticated user

        var result = await service.DecideAsync(first.ApprovalRequestId!.Value, new ToolApprovalDecisionInput(ToolApprovalDecisionType.Approve));

        result.Outcome.Should().Be(ToolApprovalDecisionOutcome.Forbidden, "a decision has no authority without an authenticated user");
    }

    [Fact]
    public async Task DecideAsync_Should_ReturnNotFound_When_RequestIdUnknown()
    {
        var f = new Fixture();
        var service = f.Build();

        var result = await service.DecideAsync(Guid.NewGuid(), new ToolApprovalDecisionInput(ToolApprovalDecisionType.Approve));

        result.Outcome.Should().Be(ToolApprovalDecisionOutcome.NotFound);
    }

    [Fact]
    public async Task DecideAsync_Should_ReturnExpiredAndFlipStatus_When_PastExpiry()
    {
        var f = new Fixture();
        var service = f.Build();
        var first = await service.GateAsync(Medium());

        f.Clock.UtcNow = f.Clock.UtcNow.AddHours(1); // well past the 15-minute window

        var result = await service.DecideAsync(first.ApprovalRequestId!.Value, new ToolApprovalDecisionInput(ToolApprovalDecisionType.Approve));

        result.Outcome.Should().Be(ToolApprovalDecisionOutcome.Expired);
        f.RequestStore.Requests.Single().Status.Should().Be(ToolApprovalRequestStatus.Expired);
    }

    [Fact]
    public async Task DecideAsync_Should_ReturnAlreadyDecided_When_DecidedTwice()
    {
        var f = new Fixture();
        var service = f.Build();
        var first = await service.GateAsync(Medium());
        var requestId = first.ApprovalRequestId!.Value;

        await service.DecideAsync(requestId, new ToolApprovalDecisionInput(ToolApprovalDecisionType.Approve));
        var second = await service.DecideAsync(requestId, new ToolApprovalDecisionInput(ToolApprovalDecisionType.Approve));

        second.Outcome.Should().Be(ToolApprovalDecisionOutcome.AlreadyDecided);
    }

    [Fact]
    public async Task DecideAsync_Should_ReturnForbidden_When_DecidingUserDiffersFromRequestingUser()
    {
        var f = new Fixture();
        var service = f.Build();
        var first = await service.GateAsync(Medium()); // requesting user = the fixture's user

        f.User.UserId = Guid.NewGuid(); // a different user attempts the decision

        var result = await service.DecideAsync(first.ApprovalRequestId!.Value, new ToolApprovalDecisionInput(ToolApprovalDecisionType.Approve));

        result.Outcome.Should().Be(ToolApprovalDecisionOutcome.Forbidden, "for consumer flows the deciding user must equal the requesting user");
        f.RequestStore.Requests.Single().Status.Should().Be(ToolApprovalRequestStatus.Pending, "a forbidden decision must not change the request");
    }

    [Fact]
    public async Task DecideAsync_Should_RecordRejection_When_Reject()
    {
        var f = new Fixture();
        var service = f.Build();
        var first = await service.GateAsync(Medium());

        var result = await service.DecideAsync(first.ApprovalRequestId!.Value, new ToolApprovalDecisionInput(ToolApprovalDecisionType.Reject, "no thanks"));

        result.Outcome.Should().Be(ToolApprovalDecisionOutcome.Rejected);
        var request = f.RequestStore.Requests.Single();
        request.Status.Should().Be(ToolApprovalRequestStatus.Rejected);
        request.DecidedByUserId.Should().Be(f.User.UserId);
        request.DecidedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task DecideAsync_Should_ApproveMediumWithoutProposalPath_When_NoLinkedProposal()
    {
        var f = new Fixture();
        var service = f.Build();
        var first = await service.GateAsync(Medium());

        var result = await service.DecideAsync(first.ApprovalRequestId!.Value, new ToolApprovalDecisionInput(ToolApprovalDecisionType.Approve));

        result.Outcome.Should().Be(ToolApprovalDecisionOutcome.Approved);
        result.ProposalId.Should().BeNull("a Medium approval is recorded, then consumed by the gate — it does not go through a proposal");
        f.ApprovalService.ApprovedProposalIds.Should().BeEmpty("Medium never runs the proposal-approval money path");
    }

    // ----- DecideAsync: High routes through the proposal-approval path ---------------------------

    [Fact]
    public async Task DecideAsync_Should_RouteHighThroughPolicyAndProposalApproval_When_ApprovedAndAuthorized()
    {
        var f = new Fixture();
        var service = f.Build();
        var queued = await service.GateAsync(High());
        var proposalId = queued.ProposalId!.Value;
        var requestId = queued.ApprovalRequestId!.Value;

        f.ApprovalService.Detail = ProposalDetail(proposalId);

        var result = await service.DecideAsync(requestId, new ToolApprovalDecisionInput(ToolApprovalDecisionType.Approve));

        result.Outcome.Should().Be(ToolApprovalDecisionOutcome.Approved);
        result.ProposalId.Should().Be(proposalId);

        f.ApprovalService.ApprovedProposalIds.Should().ContainSingle().Which.Should().Be(proposalId,
            "an in-session High approval takes the same money path as a queue approval");
        f.Policy.LastContext!.ProposalId.Should().Be(proposalId, "the policy is consulted for the linked proposal");

        var request = f.RequestStore.Requests.Single(r => r.Id == requestId);
        request.Status.Should().Be(ToolApprovalRequestStatus.Approved);
        request.ConsumedAt.Should().NotBeNull("High executes via the proposal, so the request is marked consumed");
    }

    [Fact]
    public async Task DecideAsync_Should_ReturnForbiddenAndNotExecute_When_HighAndPolicyDenies()
    {
        var f = new Fixture();
        var service = f.Build();
        var queued = await service.GateAsync(High());
        var proposalId = queued.ProposalId!.Value;
        var requestId = queued.ApprovalRequestId!.Value;

        f.ApprovalService.Detail = ProposalDetail(proposalId);
        f.Policy.Result = ApprovalAuthorization.Denied("separation of duties");

        var result = await service.DecideAsync(requestId, new ToolApprovalDecisionInput(ToolApprovalDecisionType.Approve));

        result.Outcome.Should().Be(ToolApprovalDecisionOutcome.Forbidden);
        f.ApprovalService.ApprovedProposalIds.Should().BeEmpty("a policy denial must never reach the money path");
        f.RequestStore.Requests.Single(r => r.Id == requestId).Status.Should().Be(ToolApprovalRequestStatus.Pending);
    }

    [Fact]
    public async Task DecideAsync_Should_PropagateExecutionFailure_When_HighApproveThrows()
    {
        var f = new Fixture();
        var service = f.Build();
        var queued = await service.GateAsync(High());
        var proposalId = queued.ProposalId!.Value;
        var requestId = queued.ApprovalRequestId!.Value;

        f.ApprovalService.Detail = ProposalDetail(proposalId);
        f.ApprovalService.ApproveThrows = new ProposalExecutionFailedException(proposalId, "insufficient funds");

        var act = () => service.DecideAsync(requestId, new ToolApprovalDecisionInput(ToolApprovalDecisionType.Approve));

        await act.Should().ThrowAsync<ProposalExecutionFailedException>(
            "the endpoint maps the money-path failure exactly like the proposal-approval endpoint");
        f.RequestStore.Requests.Single(r => r.Id == requestId).Status.Should().Be(ToolApprovalRequestStatus.Pending,
            "a failed High dispatch leaves the request undecided, not Approved");
    }

    [Fact]
    public async Task DecideAsync_Should_ReturnNotFound_When_HighLinkedProposalMissing()
    {
        var f = new Fixture();
        var service = f.Build();
        var queued = await service.GateAsync(High());
        var requestId = queued.ApprovalRequestId!.Value;

        f.ApprovalService.Detail = null; // the linked proposal is no longer available

        var result = await service.DecideAsync(requestId, new ToolApprovalDecisionInput(ToolApprovalDecisionType.Approve));

        result.Outcome.Should().Be(ToolApprovalDecisionOutcome.NotFound);
        f.ApprovalService.ApprovedProposalIds.Should().BeEmpty();
    }
}
