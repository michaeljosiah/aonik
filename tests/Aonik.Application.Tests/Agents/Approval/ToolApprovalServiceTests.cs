using System.Text.Json;

using Aonik.Agents.Services;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Multitenancy;

using FluentAssertions;

namespace Aonik.Application.Tests.Agents.Approval;

/// <summary>
/// Spec 032 §7.5 — the High-tier router (<see cref="ToolApprovalService"/>) the gate decorator
/// delegates to. These tests pin the slice's behaviour: Low / Medium report
/// <see cref="ToolGateDecision.ApprovedInline"/> (the decorator handles them in-band); a correctly
/// configured High tool creates exactly one Proposed / High proposal carrying the verbatim argument
/// payload and returns <see cref="ToolGateDecision.Queued"/> with its id; and the fail-closed
/// <see cref="ToolGateDecision.Refused"/> paths (no ProposalType, no resolvable tenant) never touch
/// the proposal store — so the money call can never run ungated.
/// </summary>
public class ToolApprovalServiceTests
{
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

    private sealed class TestTenantProvider(Guid? tenantId) : ITenantProvider
    {
        public Guid GetCurrentTenantId() => tenantId ?? throw new InvalidOperationException("no tenant in scope");

        public bool TryGetCurrentTenantId(out Guid id)
        {
            id = tenantId ?? Guid.Empty;
            return tenantId.HasValue;
        }
    }

    private static ToolGateContext HighContext(
        string toolName,
        string? proposalType,
        IDictionary<string, object?>? arguments = null) =>
        new(
            toolName,
            new ToolApprovalOptions(ToolApprovalTier.High, "Capture a payment", proposalType),
            arguments ?? new Dictionary<string, object?>());

    [Fact]
    public async Task GateAsync_Should_ReturnApprovedInline_When_TierIsLow()
    {
        var store = new RecordingProposalStore();
        var service = new ToolApprovalService(store, new TestTenantProvider(Guid.NewGuid()));

        var outcome = await service.GateAsync(new ToolGateContext(
            "pf_set_preference",
            new ToolApprovalOptions(ToolApprovalTier.Low, "Set a preference"),
            new Dictionary<string, object?>()));

        outcome.Decision.Should().Be(ToolGateDecision.ApprovedInline);
        outcome.ProposalId.Should().BeNull();
        store.Created.Should().BeEmpty("Low-tier tools run in-band and are never marshalled");
    }

    [Fact]
    public async Task GateAsync_Should_ReturnApprovedInline_When_TierIsMedium()
    {
        var store = new RecordingProposalStore();
        var service = new ToolApprovalService(store, new TestTenantProvider(Guid.NewGuid()));

        var outcome = await service.GateAsync(new ToolGateContext(
            "finance_create_invoice",
            new ToolApprovalOptions(ToolApprovalTier.Medium, "Create an invoice"),
            new Dictionary<string, object?>()));

        outcome.Decision.Should().Be(ToolGateDecision.ApprovedInline);
        store.Created.Should().BeEmpty("Medium is handled by the decorator, not marshalled here");
    }

    [Fact]
    public async Task GateAsync_Should_CreateProposedHighProposalAndQueue_When_HighWithProposalTypeAndTenant()
    {
        var tenantId = Guid.NewGuid();
        var store = new RecordingProposalStore();
        var service = new ToolApprovalService(store, new TestTenantProvider(tenantId));

        var paymentIntentId = Guid.NewGuid();
        var arguments = new Dictionary<string, object?> { ["paymentIntentId"] = paymentIntentId };

        var outcome = await service.GateAsync(
            HighContext("finance_capture_payment", "Finance.CapturePayment", arguments));

        outcome.Decision.Should().Be(ToolGateDecision.Queued);
        outcome.ProposalId.Should().NotBeNull();

        store.Created.Should().ContainSingle("a High tool marshals into exactly one durable proposal");
        var created = store.Created[0];
        created.Id.Should().Be(outcome.ProposalId!.Value, "the queued id must reference the created proposal");
        created.TenantId.Should().Be(tenantId, "the proposal must be scoped to the current tenant");
        created.ProposalType.Should().Be("Finance.CapturePayment");
        created.RiskTier.Should().Be("High");
        created.ImpactSummary.Should().Be("Capture a payment");
        created.ProposedByAgentId.Should().Be(Guid.Empty);

        // The payload round-trips the model arguments by their verbatim keys so the IProposalHandler
        // can read them back when the proposal is approved.
        using var doc = JsonDocument.Parse(created.PayloadJson);
        doc.RootElement.GetProperty("paymentIntentId").GetGuid().Should().Be(paymentIntentId);
    }

    [Fact]
    public async Task GateAsync_Should_RefuseWithoutTouchingStore_When_HighHasNoProposalType()
    {
        var store = new RecordingProposalStore();
        var service = new ToolApprovalService(store, new TestTenantProvider(Guid.NewGuid()));

        var outcome = await service.GateAsync(HighContext("finance_capture_payment", proposalType: null));

        outcome.Decision.Should().Be(ToolGateDecision.Refused);
        outcome.ProposalId.Should().BeNull();
        outcome.Reason.Should().NotBeNullOrWhiteSpace();
        store.Created.Should().BeEmpty("a misconfigured High tool must never create a proposal");
    }

    [Fact]
    public async Task GateAsync_Should_RefuseWithoutTouchingStore_When_NoCurrentTenant()
    {
        var store = new RecordingProposalStore();
        var service = new ToolApprovalService(store, new TestTenantProvider(tenantId: null));

        var outcome = await service.GateAsync(HighContext("finance_capture_payment", "Finance.CapturePayment"));

        outcome.Decision.Should().Be(ToolGateDecision.Refused);
        outcome.ProposalId.Should().BeNull();
        store.Created.Should().BeEmpty("with no tenant in scope the proposal cannot be created");
    }
}
