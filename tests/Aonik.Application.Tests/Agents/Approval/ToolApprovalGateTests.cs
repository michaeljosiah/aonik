using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Agents.Approval;

using FluentAssertions;

using Microsoft.Extensions.AI;

namespace Aonik.Application.Tests.Agents.Approval;

/// <summary>
/// Spec 032 (finding C3) — the fail-closed tool approval gate. These tests pin the gate's
/// four behaviours: read-looking tools pass through untouched, classified mutations are
/// wrapped so they cannot run ungated, an unclassified mutating-looking tool throws, and every
/// gated invocation is audited.
/// </summary>
public class ToolApprovalGateTests
{
    private sealed class StubManifest : IToolApprovalManifest
    {
        private readonly IReadOnlyDictionary<string, ToolClassification> _map;

        public StubManifest(IReadOnlyDictionary<string, ToolClassification> map) => _map = map;

        public string Module => "Stub";

        public ToolClassification? Classify(string toolName) =>
            _map.TryGetValue(toolName, out var classification) ? classification : null;
    }

    private sealed class RecordingAuditSink : IToolApprovalAuditSink
    {
        public List<ToolApprovalAuditEntry> Entries { get; } = new();

        public void Record(ToolApprovalAuditEntry entry) => Entries.Add(entry);
    }

    /// <summary>
    /// Stand-in for the request-scoped High-tier router. Records the context it was handed so a
    /// test can assert the decorator marshals the real tool name / ProposalType / arguments, and
    /// returns a fixed outcome so both the queued and refused branches can be exercised.
    /// </summary>
    private sealed class StubApprovalService : IToolApprovalService
    {
        private readonly ToolGateOutcome _outcome;

        public StubApprovalService(ToolGateOutcome outcome) => _outcome = outcome;

        public int Calls { get; private set; }

        public ToolGateContext? LastContext { get; private set; }

        public Task<ToolGateOutcome> GateAsync(ToolGateContext context, CancellationToken cancellationToken = default)
        {
            Calls++;
            LastContext = context;
            return Task.FromResult(_outcome);
        }

        // The decorator never calls DecideAsync (that is the transport endpoint's job), so the gate
        // tests only need it to satisfy the interface. Returns a benign Approved result.
        public Task<ToolApprovalDecisionResult> DecideAsync(
            Guid approvalRequestId,
            ToolApprovalDecisionInput decision,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ToolApprovalDecisionResult(
                approvalRequestId, ToolApprovalDecisionOutcome.Approved, ProposalId: null, Message: null));
    }

    /// <summary>Minimal provider that resolves only <see cref="IToolApprovalService"/> — mirrors how the
    /// decorator pulls the router lazily from the agent-build-time <see cref="IServiceProvider"/>.</summary>
    private sealed class StubServiceProvider : IServiceProvider
    {
        private readonly object? _approvalService;

        public StubServiceProvider(object? approvalService) => _approvalService = approvalService;

        public object? GetService(Type serviceType) =>
            serviceType == typeof(IToolApprovalService) ? _approvalService : null;
    }

    private static ToolApprovalGate CreateGate(
        IReadOnlyDictionary<string, ToolClassification> classifications,
        out RecordingAuditSink sink)
    {
        sink = new RecordingAuditSink();
        return new ToolApprovalGate(new[] { new StubManifest(classifications) }, sink);
    }

    private static Dictionary<string, ToolClassification> Empty() => new(StringComparer.Ordinal);

    [Fact]
    public void Gate_Should_PassToolThroughUnchanged_When_UnclassifiedAndReadLooking()
    {
        var gate = CreateGate(Empty(), out _);
        var read = AIFunctionFactory.Create(() => "ok", "finance_get_invoice");

        var gated = gate.Gate(read);

        gated.Should().BeSameAs(read, "a read-looking, unclassified tool must pass through untouched");
    }

    [Fact]
    public void Gate_Should_PassToolThroughUnchanged_When_ExplicitlyClassifiedReadOnly()
    {
        var read = AIFunctionFactory.Create(() => "ok", "finance_list_ledgers");
        var gate = CreateGate(
            new Dictionary<string, ToolClassification>(StringComparer.Ordinal)
            {
                ["finance_list_ledgers"] = ToolClassification.ReadOnly,
            },
            out _);

        var gated = gate.Gate(read);

        gated.Should().BeSameAs(read);
    }

    [Fact]
    public void Gate_Should_Throw_When_ToolIsMutatingLookingButUnclassified()
    {
        var gate = CreateGate(Empty(), out _);
        var rogue = AIFunctionFactory.Create(() => "ok", "finance_delete_everything");

        var act = () => gate.Gate(rogue);

        act.Should().Throw<ToolNotClassifiedException>()
            .Which.ToolName.Should().Be("finance_delete_everything");
    }

    [Fact]
    public void Gate_Should_WrapToolWhilePreservingName_When_ClassifiedMutating()
    {
        var tool = AIFunctionFactory.Create(() => "ok", "finance_create_invoice");
        var gate = CreateGate(
            new Dictionary<string, ToolClassification>(StringComparer.Ordinal)
            {
                ["finance_create_invoice"] = ToolClassification.Mutating(
                    new ToolApprovalOptions(ToolApprovalTier.Medium, "Create a draft invoice")),
            },
            out _);

        var gated = gate.Gate(tool);

        gated.Should().NotBeSameAs(tool, "a classified mutation must be wrapped, not returned as-is");
        gated.Name.Should().Be("finance_create_invoice", "the wrapper must preserve the tool name for the model");
    }

    [Fact]
    public async Task GatedMediumTool_Should_RefuseWithoutInvokingInner_When_Invoked()
    {
        var invoked = 0;
        var tool = AIFunctionFactory.Create(() => { invoked++; return "created"; }, "finance_create_invoice");
        var gate = CreateGate(
            new Dictionary<string, ToolClassification>(StringComparer.Ordinal)
            {
                ["finance_create_invoice"] = ToolClassification.Mutating(
                    new ToolApprovalOptions(ToolApprovalTier.Medium, "Create a draft invoice")),
            },
            out var sink);

        var gated = (AIFunction)gate.Gate(tool);
        var result = await gated.InvokeAsync(new AIFunctionArguments(), CancellationToken.None);

        invoked.Should().Be(0, "a Medium-tier mutation must not run its inner domain call ungated");
        result.Should().BeOfType<ToolApprovalRequiredResult>();
        var approval = (ToolApprovalRequiredResult)result!;
        approval.Executed.Should().BeFalse();
        approval.Status.Should().Be(ToolApprovalRequiredResult.RequiresApprovalStatus);
        approval.Tier.Should().Be("Medium");

        sink.Entries.Should().ContainSingle();
        sink.Entries[0].Tool.Should().Be("finance_create_invoice");
        sink.Entries[0].Tier.Should().Be(ToolApprovalTier.Medium);
        sink.Entries[0].Executed.Should().BeFalse();
    }

    [Fact]
    public async Task GatedMediumTool_Should_ReturnRequiresApprovalCarryingRequestId_When_GatePendsApproval()
    {
        var invoked = 0;
        var tool = AIFunctionFactory.Create(() => { invoked++; return "created"; }, "finance_create_invoice");
        var gate = CreateGate(
            new Dictionary<string, ToolClassification>(StringComparer.Ordinal)
            {
                ["finance_create_invoice"] = ToolClassification.Mutating(
                    new ToolApprovalOptions(ToolApprovalTier.Medium, "Create a draft invoice")),
            },
            out var sink);

        // The gate persisted a Pending request and handed back its id — the decorator must surface it
        // (so the user can approve via DecideAsync) and must NOT run the inner domain call.
        var requestId = Guid.NewGuid();
        var approvals = new StubApprovalService(
            new ToolGateOutcome(ToolGateDecision.PendingApproval, ApprovalRequestId: requestId, Summary: "Create a draft invoice"));
        var gated = (AIFunction)gate.Gate(tool, new StubServiceProvider(approvals));

        var result = await gated.InvokeAsync(new AIFunctionArguments(), CancellationToken.None);

        invoked.Should().Be(0, "a pending Medium confirm must not run its inner domain call");
        approvals.Calls.Should().Be(1);
        result.Should().BeOfType<ToolApprovalRequiredResult>();
        var approval = (ToolApprovalRequiredResult)result!;
        approval.ApprovalRequestId.Should().Be(requestId, "the presentation layer routes the user's decision to DecideAsync by this id");
        approval.Executed.Should().BeFalse();

        sink.Entries.Should().ContainSingle();
        sink.Entries[0].Outcome.Should().Be("pending-approval");
        sink.Entries[0].Executed.Should().BeFalse();
    }

    [Fact]
    public async Task GatedMediumTool_Should_RunInnerOnce_When_GateConsumesAnExistingApproval()
    {
        var invoked = 0;
        var tool = AIFunctionFactory.Create(() => { invoked++; return "created"; }, "finance_create_invoice");
        var gate = CreateGate(
            new Dictionary<string, ToolClassification>(StringComparer.Ordinal)
            {
                ["finance_create_invoice"] = ToolClassification.Mutating(
                    new ToolApprovalOptions(ToolApprovalTier.Medium, "Create a draft invoice")),
            },
            out var sink);

        // The gate found and consumed a matching server-validated approval (args-hash bound) →
        // ApprovedInline. The decorator must run the inner domain call exactly once.
        var approvals = new StubApprovalService(
            new ToolGateOutcome(ToolGateDecision.ApprovedInline, ApprovalRequestId: Guid.NewGuid(), Summary: "Create a draft invoice"));
        var gated = (AIFunction)gate.Gate(tool, new StubServiceProvider(approvals));

        var result = await gated.InvokeAsync(new AIFunctionArguments(), CancellationToken.None);

        invoked.Should().Be(1, "a consumed approval lets the Medium tool run in-band, once");
        (result?.ToString() ?? string.Empty).Should().Contain("created");

        sink.Entries.Should().ContainSingle();
        sink.Entries[0].Tier.Should().Be(ToolApprovalTier.Medium);
        sink.Entries[0].Outcome.Should().Be("executed-inline-approved");
        sink.Entries[0].Executed.Should().BeTrue();
    }

    [Fact]
    public async Task GatedHighTool_Should_RefuseWithoutInvokingInner_When_Invoked()
    {
        var invoked = 0;
        var tool = AIFunctionFactory.Create(() => { invoked++; return "captured"; }, "finance_capture_payment");
        var gate = CreateGate(
            new Dictionary<string, ToolClassification>(StringComparer.Ordinal)
            {
                ["finance_capture_payment"] = ToolClassification.Mutating(
                    new ToolApprovalOptions(ToolApprovalTier.High, "Capture a payment", "Finance.CapturePayment")),
            },
            out var sink);

        // No service provider → the decorator cannot resolve the marshalling service, so the High
        // path must fail closed exactly like Medium rather than run the money call ungated.
        var gated = (AIFunction)gate.Gate(tool);
        var result = await gated.InvokeAsync(new AIFunctionArguments(), CancellationToken.None);

        invoked.Should().Be(0, "a High-tier money-movement tool must never run in-band");
        result.Should().BeOfType<ToolApprovalRequiredResult>();
        ((ToolApprovalRequiredResult)result!).Tier.Should().Be("High");

        sink.Entries.Should().ContainSingle();
        sink.Entries[0].Executed.Should().BeFalse();
        sink.Entries[0].Outcome.Should().Be("blocked-requires-approval");
    }

    [Fact]
    public async Task GatedHighTool_Should_MarshalIntoProposalAndReturnQueued_When_ApprovalServiceQueuesIt()
    {
        var invoked = 0;
        var tool = AIFunctionFactory.Create(() => { invoked++; return "captured"; }, "finance_capture_payment");
        var gate = CreateGate(
            new Dictionary<string, ToolClassification>(StringComparer.Ordinal)
            {
                ["finance_capture_payment"] = ToolClassification.Mutating(
                    new ToolApprovalOptions(ToolApprovalTier.High, "Capture a payment", "Finance.CapturePayment")),
            },
            out var sink);

        var proposalId = Guid.NewGuid();
        var approvals = new StubApprovalService(
            new ToolGateOutcome(ToolGateDecision.Queued, ProposalId: proposalId, Summary: "Capture a payment"));
        var gated = (AIFunction)gate.Gate(tool, new StubServiceProvider(approvals));

        var paymentIntentId = Guid.NewGuid();
        var args = new AIFunctionArguments { ["paymentIntentId"] = paymentIntentId };
        var result = await gated.InvokeAsync(args, CancellationToken.None);

        invoked.Should().Be(0, "a High tool is marshalled into a durable proposal, never run in-band");

        approvals.Calls.Should().Be(1, "the decorator must delegate the High tool to the marshalling service");
        approvals.LastContext.Should().NotBeNull();
        approvals.LastContext!.ToolName.Should().Be("finance_capture_payment");
        approvals.LastContext.Options.ProposalType.Should().Be("Finance.CapturePayment", "the ProposalType must flow to the router so it can pick the right handler");
        approvals.LastContext.Arguments.Should().ContainKey("paymentIntentId", "the model-supplied arguments become the proposal payload");

        result.Should().BeOfType<ToolApprovalQueuedResult>();
        var queued = (ToolApprovalQueuedResult)result!;
        queued.Status.Should().Be(ToolApprovalQueuedResult.QueuedStatus);
        queued.ProposalId.Should().Be(proposalId, "the agent must be able to reference the created proposal");
        queued.Executed.Should().BeFalse();
        queued.Tier.Should().Be("High");

        sink.Entries.Should().ContainSingle();
        sink.Entries[0].Tool.Should().Be("finance_capture_payment");
        sink.Entries[0].Tier.Should().Be(ToolApprovalTier.High);
        sink.Entries[0].Executed.Should().BeFalse();
        sink.Entries[0].Outcome.Should().Be("queued-for-approval");
    }

    [Fact]
    public async Task GatedHighTool_Should_FailClosed_When_ApprovalServiceRefusesToQueue()
    {
        var invoked = 0;
        var tool = AIFunctionFactory.Create(() => { invoked++; return "captured"; }, "finance_capture_payment");
        var gate = CreateGate(
            new Dictionary<string, ToolClassification>(StringComparer.Ordinal)
            {
                ["finance_capture_payment"] = ToolClassification.Mutating(
                    new ToolApprovalOptions(ToolApprovalTier.High, "Capture a payment", "Finance.CapturePayment")),
            },
            out var sink);

        // The router declined to queue (e.g. no tenant, missing ProposalType). The decorator must
        // NOT treat a non-Queued outcome as success — it falls through to the refusal result.
        var approvals = new StubApprovalService(
            new ToolGateOutcome(ToolGateDecision.Refused, ProposalId: null, Summary: null, Reason: "no tenant in scope"));
        var gated = (AIFunction)gate.Gate(tool, new StubServiceProvider(approvals));

        var result = await gated.InvokeAsync(new AIFunctionArguments(), CancellationToken.None);

        invoked.Should().Be(0, "a refused High gate must never run the money call");
        approvals.Calls.Should().Be(1);
        result.Should().BeOfType<ToolApprovalRequiredResult>("a refused outcome fails closed, it does not queue");
        ((ToolApprovalRequiredResult)result!).Executed.Should().BeFalse();

        sink.Entries.Should().ContainSingle();
        sink.Entries[0].Executed.Should().BeFalse();
        sink.Entries[0].Outcome.Should().Be("blocked-requires-approval");
    }

    [Fact]
    public async Task GatedLowTool_Should_RunInBandAndAudit_When_Invoked()
    {
        var invoked = 0;
        var tool = AIFunctionFactory.Create(() => { invoked++; return "noted"; }, "pf_set_preference");
        var gate = CreateGate(
            new Dictionary<string, ToolClassification>(StringComparer.Ordinal)
            {
                ["pf_set_preference"] = ToolClassification.Mutating(
                    new ToolApprovalOptions(ToolApprovalTier.Low, "Set a preference")),
            },
            out var sink);

        var gated = (AIFunction)gate.Gate(tool);
        var result = await gated.InvokeAsync(new AIFunctionArguments(), CancellationToken.None);

        invoked.Should().Be(1, "a Low-tier reversible write runs in-band");
        (result?.ToString() ?? string.Empty).Should().Contain("noted");

        sink.Entries.Should().ContainSingle();
        sink.Entries[0].Tier.Should().Be(ToolApprovalTier.Low);
        sink.Entries[0].Executed.Should().BeTrue();
    }

    [Fact]
    public void GateAll_Should_GateEachToolIndependently_When_MixedSet()
    {
        var read = AIFunctionFactory.Create(() => "ok", "finance_get_invoice");
        var mutate = AIFunctionFactory.Create(() => "ok", "finance_create_invoice");
        var gate = CreateGate(
            new Dictionary<string, ToolClassification>(StringComparer.Ordinal)
            {
                ["finance_create_invoice"] = ToolClassification.Mutating(
                    new ToolApprovalOptions(ToolApprovalTier.Medium, "Create a draft invoice")),
            },
            out _);

        var gated = gate.GateAll(new AITool[] { read, mutate }).ToList();

        gated.Should().HaveCount(2);
        gated[0].Should().BeSameAs(read, "the read tool passes through");
        gated[1].Should().NotBeSameAs(mutate, "the mutating tool is wrapped");
    }

    [Fact]
    public void Gate_Should_UseFirstManifestThatClaimsTool_When_MultipleManifestsRegistered()
    {
        var sink = new RecordingAuditSink();
        var first = new StubManifest(new Dictionary<string, ToolClassification>(StringComparer.Ordinal)
        {
            ["shared_create_thing"] = ToolClassification.Mutating(
                new ToolApprovalOptions(ToolApprovalTier.High, "First wins")),
        });
        var second = new StubManifest(new Dictionary<string, ToolClassification>(StringComparer.Ordinal)
        {
            ["shared_create_thing"] = ToolClassification.Mutating(
                new ToolApprovalOptions(ToolApprovalTier.Low, "Second loses")),
        });
        var gate = new ToolApprovalGate(new[] { first, second }, sink);
        var tool = AIFunctionFactory.Create(() => "ok", "shared_create_thing");

        var gated = gate.Gate(tool);

        gated.Should().NotBeSameAs(tool, "the first manifest claims and classifies it as a mutation");
    }
}
