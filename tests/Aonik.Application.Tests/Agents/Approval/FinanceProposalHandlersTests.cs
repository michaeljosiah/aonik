using System.Text.Json;

using Aonik.Finance.Agents.Proposals;
using Aonik.Finance.Contracts.Models.Billing;
using Aonik.Finance.Contracts.Models.Payments;
using Aonik.Finance.Contracts.Services.Billing;
using Aonik.Finance.Contracts.Services.Payments;
using Aonik.Finance.Entities.Billing;
using Aonik.Finance.Entities.Payments;
using Aonik.SharedKernel.Abstractions.Agents;

using FluentAssertions;

using Moq;

namespace Aonik.Application.Tests.Agents.Approval;

/// <summary>
/// Spec 032 §7.4 — the durable-execution side of the High-tier money tools. A gated money tool is
/// never run in-band; it is marshalled into a Proposal and executed only here, after approval, via
/// the keyed <see cref="IProposalHandler"/>. These tests pin the contract that makes that safe:
/// the handler reaches the Finance service exactly once on the happy path, converges on success
/// when the entity is already in the target state (idempotent retry), and fails closed (Applied =
/// false → 422, terminal for High) for a missing entity, a wrong-state entity, or a bad payload —
/// never silently doing nothing or throwing a 500.
/// </summary>
public class FinanceProposalHandlersTests
{
    private static string PayloadJson(params (string Key, object? Value)[] entries)
    {
        var dict = new Dictionary<string, object?>();
        foreach (var (key, value) in entries)
        {
            dict[key] = value;
        }

        return JsonSerializer.Serialize(dict);
    }

    private static AgentProposalDetail Proposal(string proposalType, string payloadJson) =>
        new(
            Id: Guid.NewGuid(),
            TenantId: Guid.NewGuid(),
            ProposalType: proposalType,
            Status: "Approved",
            PayloadJson: payloadJson,
            ImpactSummary: "test");

    private static PaymentIntentResponse Intent(Guid id, PaymentStatus status) =>
        new(
            Id: id,
            OrderId: Guid.NewGuid(),
            InvoiceId: null,
            Amount: 100m,
            Currency: "USD",
            Status: status,
            Reference: "ref",
            CreatedUtc: DateTime.UtcNow);

    private static InvoiceResponse Invoice(Guid id, InvoiceStatus status) =>
        new(
            Id: id,
            CustomerId: Guid.NewGuid(),
            InvoiceNumber: "INV-1",
            Currency: "USD",
            TotalAmount: 100m,
            Status: status,
            IssuedUtc: DateTime.UtcNow,
            DueUtc: DateTime.UtcNow.AddDays(30),
            LineItems: new List<InvoiceLineItemResponse>());

    // ---- Finance.CapturePayment -------------------------------------------------------------

    [Fact]
    public async Task Capture_Should_CapturePaymentExactlyOnce_When_IntentAuthorized()
    {
        var intentId = Guid.NewGuid();
        var payment = new Mock<IPaymentService>();
        payment.Setup(p => p.GetPaymentIntentAsync(intentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Intent(intentId, PaymentStatus.Authorized));
        payment.Setup(p => p.CapturePaymentAsync(intentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Intent(intentId, PaymentStatus.Captured));

        var handler = new CapturePaymentProposalHandler(payment.Object);
        var result = await handler.HandleAsync(
            Proposal(CapturePaymentProposalHandler.ProposalTypeKey, PayloadJson(("paymentIntentId", intentId))),
            CancellationToken.None);

        result.Applied.Should().BeTrue();
        result.AppliedResourceType.Should().Be("PaymentIntent");
        result.AppliedResourceId.Should().Be(intentId);
        payment.Verify(p => p.CapturePaymentAsync(intentId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Capture_Should_ConvergeOnSuccessWithoutCapturing_When_AlreadyCaptured()
    {
        var intentId = Guid.NewGuid();
        var payment = new Mock<IPaymentService>();
        payment.Setup(p => p.GetPaymentIntentAsync(intentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Intent(intentId, PaymentStatus.Captured));

        var handler = new CapturePaymentProposalHandler(payment.Object);
        var result = await handler.HandleAsync(
            Proposal(CapturePaymentProposalHandler.ProposalTypeKey, PayloadJson(("paymentIntentId", intentId))),
            CancellationToken.None);

        result.Applied.Should().BeTrue("an already-captured intent is the idempotent success state");
        payment.Verify(p => p.CapturePaymentAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never,
            "a second approval must not capture twice");
    }

    [Fact]
    public async Task Capture_Should_FailClosed_When_IntentMissing()
    {
        var intentId = Guid.NewGuid();
        var payment = new Mock<IPaymentService>();
        payment.Setup(p => p.GetPaymentIntentAsync(intentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentIntentResponse?)null);

        var handler = new CapturePaymentProposalHandler(payment.Object);
        var result = await handler.HandleAsync(
            Proposal(CapturePaymentProposalHandler.ProposalTypeKey, PayloadJson(("paymentIntentId", intentId))),
            CancellationToken.None);

        result.Applied.Should().BeFalse();
        payment.Verify(p => p.CapturePaymentAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Capture_Should_FailClosed_When_IntentNotAuthorized()
    {
        var intentId = Guid.NewGuid();
        var payment = new Mock<IPaymentService>();
        payment.Setup(p => p.GetPaymentIntentAsync(intentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Intent(intentId, PaymentStatus.Pending));

        var handler = new CapturePaymentProposalHandler(payment.Object);
        var result = await handler.HandleAsync(
            Proposal(CapturePaymentProposalHandler.ProposalTypeKey, PayloadJson(("paymentIntentId", intentId))),
            CancellationToken.None);

        result.Applied.Should().BeFalse("only an authorized intent can be captured");
        payment.Verify(p => p.CapturePaymentAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Capture_Should_FailClosedWithoutTouchingService_When_PayloadMissingId()
    {
        var payment = new Mock<IPaymentService>(MockBehavior.Strict);

        var handler = new CapturePaymentProposalHandler(payment.Object);
        var result = await handler.HandleAsync(
            Proposal(CapturePaymentProposalHandler.ProposalTypeKey, PayloadJson(("wrongKey", Guid.NewGuid()))),
            CancellationToken.None);

        result.Applied.Should().BeFalse("a payload without paymentIntentId cannot be executed");
        // MockBehavior.Strict would throw if any service member were touched — none should be.
    }

    // ---- Finance.CancelPayment --------------------------------------------------------------

    [Fact]
    public async Task Cancel_Should_CancelPaymentExactlyOnce_When_IntentAuthorized()
    {
        var intentId = Guid.NewGuid();
        var payment = new Mock<IPaymentService>();
        payment.Setup(p => p.GetPaymentIntentAsync(intentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Intent(intentId, PaymentStatus.Authorized));
        payment.Setup(p => p.CancelPaymentAsync(intentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Intent(intentId, PaymentStatus.Cancelled));

        var handler = new CancelPaymentProposalHandler(payment.Object);
        var result = await handler.HandleAsync(
            Proposal(CancelPaymentProposalHandler.ProposalTypeKey, PayloadJson(("paymentIntentId", intentId))),
            CancellationToken.None);

        result.Applied.Should().BeTrue();
        result.AppliedResourceId.Should().Be(intentId);
        payment.Verify(p => p.CancelPaymentAsync(intentId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Cancel_Should_FailClosed_When_IntentAlreadyCaptured()
    {
        var intentId = Guid.NewGuid();
        var payment = new Mock<IPaymentService>();
        payment.Setup(p => p.GetPaymentIntentAsync(intentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Intent(intentId, PaymentStatus.Captured));

        var handler = new CancelPaymentProposalHandler(payment.Object);
        var result = await handler.HandleAsync(
            Proposal(CancelPaymentProposalHandler.ProposalTypeKey, PayloadJson(("paymentIntentId", intentId))),
            CancellationToken.None);

        result.Applied.Should().BeFalse("captured funds cannot be cancelled");
        payment.Verify(p => p.CancelPaymentAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---- Finance.CreatePaymentIntent --------------------------------------------------------

    [Fact]
    public async Task CreateIntent_Should_CreateExactlyOnce_When_PayloadValid()
    {
        var orderId = Guid.NewGuid();
        var createdId = Guid.NewGuid();
        var payment = new Mock<IPaymentService>();
        payment.Setup(p => p.CreatePaymentIntentAsync(It.IsAny<CreatePaymentIntentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Intent(createdId, PaymentStatus.Pending));

        var handler = new CreatePaymentIntentProposalHandler(payment.Object);
        var result = await handler.HandleAsync(
            Proposal(CreatePaymentIntentProposalHandler.ProposalTypeKey, PayloadJson(
                ("amount", 100.50m),
                ("currency", "USD"),
                ("reference", "ord-ref-1"),
                ("orderId", orderId))),
            CancellationToken.None);

        result.Applied.Should().BeTrue();
        result.AppliedResourceType.Should().Be("PaymentIntent");
        result.AppliedResourceId.Should().Be(createdId);
        payment.Verify(p => p.CreatePaymentIntentAsync(It.IsAny<CreatePaymentIntentRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateIntent_Should_ForwardPaymentMethod_When_PayloadIncludesIt()
    {
        var orderId = Guid.NewGuid();
        CreatePaymentIntentRequest? captured = null;
        var payment = new Mock<IPaymentService>();
        payment.Setup(p => p.CreatePaymentIntentAsync(It.IsAny<CreatePaymentIntentRequest>(), It.IsAny<CancellationToken>()))
            .Callback<CreatePaymentIntentRequest, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(Intent(Guid.NewGuid(), PaymentStatus.Pending));

        var handler = new CreatePaymentIntentProposalHandler(payment.Object);
        var result = await handler.HandleAsync(
            Proposal(CreatePaymentIntentProposalHandler.ProposalTypeKey, PayloadJson(
                ("amount", 100.50m),
                ("currency", "USD"),
                ("reference", "ord-ref-1"),
                ("orderId", orderId),
                ("paymentMethodType", "BankTransfer"))),
            CancellationToken.None);

        result.Applied.Should().BeTrue();
        captured.Should().NotBeNull();
        captured!.PaymentMethodType.Should().Be("BankTransfer"); // the agent-supplied rail is not dropped
    }

    [Fact]
    public async Task CreateIntent_Should_FailClosedWithoutTouchingService_When_PayloadMissingFields()
    {
        var payment = new Mock<IPaymentService>(MockBehavior.Strict);

        var handler = new CreatePaymentIntentProposalHandler(payment.Object);
        // Missing currency / reference / orderId.
        var result = await handler.HandleAsync(
            Proposal(CreatePaymentIntentProposalHandler.ProposalTypeKey, PayloadJson(("amount", 100m))),
            CancellationToken.None);

        result.Applied.Should().BeFalse("an incomplete payload must not create a payment intent");
    }

    // ---- Finance.MarkInvoicePaid ------------------------------------------------------------

    [Fact]
    public async Task MarkInvoicePaid_Should_MarkExactlyOnce_When_InvoiceIssued()
    {
        var invoiceId = Guid.NewGuid();
        var billing = new Mock<IBillingService>();
        billing.Setup(b => b.GetInvoiceAsync(invoiceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Invoice(invoiceId, InvoiceStatus.Issued));
        billing.Setup(b => b.MarkInvoiceAsPaidAsync(invoiceId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new MarkInvoicePaidProposalHandler(billing.Object);
        var result = await handler.HandleAsync(
            Proposal(MarkInvoicePaidProposalHandler.ProposalTypeKey, PayloadJson(("invoiceId", invoiceId))),
            CancellationToken.None);

        result.Applied.Should().BeTrue();
        result.AppliedResourceType.Should().Be("Invoice");
        result.AppliedResourceId.Should().Be(invoiceId);
        billing.Verify(b => b.MarkInvoiceAsPaidAsync(invoiceId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MarkInvoicePaid_Should_ConvergeOnSuccessWithoutMarking_When_AlreadyPaid()
    {
        var invoiceId = Guid.NewGuid();
        var billing = new Mock<IBillingService>();
        billing.Setup(b => b.GetInvoiceAsync(invoiceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Invoice(invoiceId, InvoiceStatus.Paid));

        var handler = new MarkInvoicePaidProposalHandler(billing.Object);
        var result = await handler.HandleAsync(
            Proposal(MarkInvoicePaidProposalHandler.ProposalTypeKey, PayloadJson(("invoiceId", invoiceId))),
            CancellationToken.None);

        result.Applied.Should().BeTrue("an already-paid invoice is the idempotent success state");
        billing.Verify(b => b.MarkInvoiceAsPaidAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MarkInvoicePaid_Should_FailClosed_When_InvoiceMissing()
    {
        var invoiceId = Guid.NewGuid();
        var billing = new Mock<IBillingService>();
        billing.Setup(b => b.GetInvoiceAsync(invoiceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((InvoiceResponse?)null);

        var handler = new MarkInvoicePaidProposalHandler(billing.Object);
        var result = await handler.HandleAsync(
            Proposal(MarkInvoicePaidProposalHandler.ProposalTypeKey, PayloadJson(("invoiceId", invoiceId))),
            CancellationToken.None);

        result.Applied.Should().BeFalse();
        billing.Verify(b => b.MarkInvoiceAsPaidAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
