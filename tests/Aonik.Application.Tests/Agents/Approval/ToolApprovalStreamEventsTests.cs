using System.Text.Json;

using Aonik.Agents.Services;
using Aonik.SharedKernel.Abstractions.Agents;

using FluentAssertions;

namespace Aonik.Application.Tests.Agents.Approval;

/// <summary>
/// Spec 032 transport slice — pins the machine-parseable CUSTOM events the AG-UI + playground stream
/// paths emit when the server gate refuses a mutating tool in-band. The client renders the approval
/// card from these (carrying the durable <c>approvalRequestId</c> / <c>proposalId</c>) and routes the
/// user's decision to <c>POST /ai/tool-approvals/{id}/decide</c> — so the exact wire shape matters.
/// The events are asserted through JSON serialization (the same camelCase form that goes on the wire).
/// </summary>
public class ToolApprovalStreamEventsTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [Fact]
    public void Inspect_Should_EmitApprovalRequiredEvent_When_MediumResultCarriesRequestId()
    {
        var requestId = Guid.NewGuid();
        var result = ToolApprovalRequiredResult.For(
            "finance_create_invoice",
            new ToolApprovalOptions(ToolApprovalTier.Medium, ActionKind: "Create invoice"),
            requestId);

        var signal = ToolApprovalStreamEvents.Inspect(result, toolCallId: "call_123");

        signal.RequiresApproval.Should().BeTrue();
        signal.CustomEvent.Should().NotBeNull();

        using var doc = Serialize(signal.CustomEvent!);
        var root = doc.RootElement;
        root.GetProperty("type").GetString().Should().Be("CUSTOM");
        root.GetProperty("name").GetString().Should().Be(ToolApprovalStreamEvents.ApprovalRequiredEventName);

        var value = root.GetProperty("value");
        value.GetProperty("approvalRequestId").GetGuid().Should().Be(requestId);
        value.GetProperty("toolCallId").GetString().Should().Be("call_123");
        value.GetProperty("tool").GetString().Should().Be("finance_create_invoice");
        value.GetProperty("tier").GetString().Should().Be("Medium");
        value.GetProperty("actionKind").GetString().Should().Be("Create invoice");
        value.GetProperty("status").GetString().Should().Be(ToolApprovalRequiredResult.RequiresApprovalStatus);
    }

    [Fact]
    public void Inspect_Should_FlagApprovalButEmitNoEvent_When_MediumResultHasNoRequestId()
    {
        // Fail-closed overload: no gate service / no resolvable tenant ⇒ no durable request persisted.
        var result = ToolApprovalRequiredResult.For(
            "finance_create_invoice",
            new ToolApprovalOptions(ToolApprovalTier.Medium, ActionKind: "Create invoice"));

        var signal = ToolApprovalStreamEvents.Inspect(result, toolCallId: "call_123");

        signal.RequiresApproval.Should().BeTrue("a gated-but-not-executed mutation still requires approval");
        signal.CustomEvent.Should().BeNull("there is no durable request id for the user to route a decision to");
    }

    [Fact]
    public void Inspect_Should_EmitApprovalQueuedEvent_When_HighResultIsQueued()
    {
        var proposalId = Guid.NewGuid();
        var result = ToolApprovalQueuedResult.For(
            "finance_capture_payment",
            new ToolApprovalOptions(
                ToolApprovalTier.High,
                ActionKind: "Capture payment",
                ProposalType: "Finance.CapturePayment"),
            proposalId,
            summary: "Capture $500 for invoice INV-1");

        var signal = ToolApprovalStreamEvents.Inspect(result, toolCallId: "call_456");

        signal.RequiresApproval.Should().BeTrue();
        signal.CustomEvent.Should().NotBeNull();

        using var doc = Serialize(signal.CustomEvent!);
        var root = doc.RootElement;
        root.GetProperty("type").GetString().Should().Be("CUSTOM");
        root.GetProperty("name").GetString().Should().Be(ToolApprovalStreamEvents.ApprovalQueuedEventName);

        var value = root.GetProperty("value");
        value.GetProperty("proposalId").GetGuid().Should().Be(proposalId);
        value.GetProperty("toolCallId").GetString().Should().Be("call_456");
        value.GetProperty("tool").GetString().Should().Be("finance_capture_payment");
        value.GetProperty("tier").GetString().Should().Be("High");
        value.GetProperty("actionKind").GetString().Should().Be("Capture payment");
        value.GetProperty("status").GetString().Should().Be(ToolApprovalQueuedResult.QueuedStatus);
    }

    [Fact]
    public void Inspect_Should_ReturnNone_When_ResultIsOrdinaryToolOutput()
    {
        ToolApprovalStreamEvents.Inspect("some ordinary tool output", "call_1")
            .Should().Be(ToolApprovalSignal.None);
    }

    [Fact]
    public void Inspect_Should_ReturnNone_When_ResultIsNull()
    {
        ToolApprovalStreamEvents.Inspect(functionResult: null, "call_1")
            .Should().Be(ToolApprovalSignal.None);
    }

    private static JsonDocument Serialize(object value) =>
        JsonDocument.Parse(JsonSerializer.Serialize(value, JsonOptions));
}
