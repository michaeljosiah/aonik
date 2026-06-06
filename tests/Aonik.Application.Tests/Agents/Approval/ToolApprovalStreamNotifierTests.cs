using Aonik.Agents.Services;
using Aonik.SharedKernel.Abstractions.Agents;

using FluentAssertions;

namespace Aonik.Application.Tests.Agents.Approval;

/// <summary>
/// Spec 032 §7.7 — the request-scoped buffer that bridges a gated tool's approval card to the active
/// client's stream regardless of how deeply the tool was nested inside a sub-agent. Pins the
/// Record/Drain contract <c>AguiStreamPipeline</c> relies on: drain returns everything once, then
/// clears, so a later run never re-emits a stale card.
/// </summary>
public class ToolApprovalStreamNotifierTests
{
    [Fact]
    public void Drain_Should_ReturnRecordedResultsThenClear()
    {
        var notifier = new ToolApprovalStreamNotifier();
        var pending = ToolApprovalRequiredResult.For(
            "finance_create_invoice",
            new ToolApprovalOptions(ToolApprovalTier.Medium, "Create a draft invoice"),
            Guid.NewGuid());
        var queued = ToolApprovalQueuedResult.For(
            "finance_capture_payment",
            new ToolApprovalOptions(ToolApprovalTier.High, "Capture a payment", "Finance.CapturePayment"),
            Guid.NewGuid(),
            Guid.NewGuid());

        notifier.Record(pending);
        notifier.Record(queued);

        notifier.Drain().Should().HaveCount(2).And.ContainInOrder(pending, queued);

        // Single-use: a second drain is empty, so the pipeline never re-emits a card on a later run.
        notifier.Drain().Should().BeEmpty();
    }

    [Fact]
    public void Record_Should_IgnoreNull()
    {
        var notifier = new ToolApprovalStreamNotifier();

        notifier.Record(null!);

        notifier.Drain().Should().BeEmpty();
    }
}
