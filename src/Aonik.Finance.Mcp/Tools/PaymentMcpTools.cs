using System.ComponentModel;
using Aonik.Finance.Contracts.Models.Payments;
using Aonik.Finance.Contracts.Services.Payments;
using ModelContextProtocol.Server;

namespace Aonik.Finance.Mcp.Tools;

/// <summary>
/// MCP tools for payment intent operations.
/// Domain services are injected via DI into method parameters.
/// </summary>
[McpServerToolType]
public static class PaymentMcpTools
{
    [McpServerTool(Name = "finance_get_payment_intent"), Description("Retrieves a payment intent by its unique identifier.")]
    public static async Task<PaymentIntentResponse?> GetPaymentIntent(
        IPaymentService paymentService,
        [Description("The unique identifier (GUID) of the payment intent")] Guid paymentIntentId,
        CancellationToken cancellationToken = default)
    {
        return await paymentService.GetPaymentIntentAsync(paymentIntentId, cancellationToken);
    }

    [McpServerTool(Name = "finance_create_payment_intent"), Description("Creates a new payment intent for an order. Links to an optional invoice.")]
    public static async Task<PaymentIntentResponse> CreatePaymentIntent(
        IPaymentService paymentService,
        [Description("Payment amount")] decimal amount,
        [Description("ISO 4217 currency code (e.g. USD, NGN)")] string currency,
        [Description("Payment reference string")] string reference,
        [Description("The order ID this payment is for")] Guid orderId,
        [Description("Optional invoice ID to link to")] Guid? invoiceId = null,
        CancellationToken cancellationToken = default)
    {
        var request = new CreatePaymentIntentRequest(amount, currency, reference, orderId, invoiceId);
        return await paymentService.CreatePaymentIntentAsync(request, cancellationToken);
    }

    [McpServerTool(Name = "finance_capture_payment"), Description("Captures (completes) a payment intent, transitioning it to a captured/paid state.")]
    public static async Task<PaymentIntentResponse> CapturePayment(
        IPaymentService paymentService,
        [Description("The unique identifier (GUID) of the payment intent to capture")] Guid paymentIntentId,
        CancellationToken cancellationToken = default)
    {
        return await paymentService.CapturePaymentAsync(paymentIntentId, cancellationToken);
    }

    [McpServerTool(Name = "finance_cancel_payment"), Description("Cancels a payment intent, transitioning it to a cancelled state.")]
    public static async Task<PaymentIntentResponse> CancelPayment(
        IPaymentService paymentService,
        [Description("The unique identifier (GUID) of the payment intent to cancel")] Guid paymentIntentId,
        CancellationToken cancellationToken = default)
    {
        return await paymentService.CancelPaymentAsync(paymentIntentId, cancellationToken);
    }
}
