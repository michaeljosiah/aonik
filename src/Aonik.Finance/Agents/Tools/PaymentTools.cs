using System.ComponentModel;
using Aonik.Finance.Contracts.Models.Payments;
using Aonik.Finance.Contracts.Services.Payments;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Finance.Agents.Tools;

/// <summary>
/// AI agent tools for payment intent operations.
/// </summary>
internal sealed class PaymentTools
{
    private readonly IPaymentService _paymentService;

    private PaymentTools(IPaymentService paymentService) => _paymentService = paymentService;

    [Description("Retrieves a payment intent by its unique identifier. Returns amount, currency, status, and linked order/invoice.")]
    public async Task<PaymentIntentResponse?> GetPaymentIntent(
        [Description("The unique identifier (GUID) of the payment intent to retrieve")] Guid paymentIntentId,
        CancellationToken cancellationToken = default)
    {
        return await _paymentService.GetPaymentIntentAsync(paymentIntentId, cancellationToken);
    }

    [Description("Creates a new payment intent to fund an order. Returns the created payment intent with its status.")]
    public async Task<PaymentIntentResponse> CreatePaymentIntent(
        [Description("The payment amount")] decimal amount,
        [Description("ISO 4217 currency code (e.g. USD, NGN)")] string currency,
        [Description("Payment reference string")] string reference,
        [Description("The order ID this payment is funding")] Guid orderId,
        [Description("Optional invoice ID linked to this payment")] Guid? invoiceId,
        CancellationToken cancellationToken = default)
    {
        var request = new CreatePaymentIntentRequest(amount, currency, reference, orderId, invoiceId);
        return await _paymentService.CreatePaymentIntentAsync(request, cancellationToken);
    }

    [Description("Captures (completes) a payment intent, moving funds. Returns the updated payment intent.")]
    public async Task<PaymentIntentResponse> CapturePayment(
        [Description("The unique identifier (GUID) of the payment intent to capture")] Guid paymentIntentId,
        CancellationToken cancellationToken = default)
    {
        return await _paymentService.CapturePaymentAsync(paymentIntentId, cancellationToken);
    }

    [Description("Cancels a payment intent. Returns the updated payment intent with cancelled status.")]
    public async Task<PaymentIntentResponse> CancelPayment(
        [Description("The unique identifier (GUID) of the payment intent to cancel")] Guid paymentIntentId,
        CancellationToken cancellationToken = default)
    {
        return await _paymentService.CancelPaymentAsync(paymentIntentId, cancellationToken);
    }

    /// <summary>
    /// Creates <see cref="AITool"/> instances for all payment tools.
    /// </summary>
    public static IEnumerable<AITool> CreateAll(IServiceProvider serviceProvider)
    {
        var tools = new PaymentTools(serviceProvider.GetRequiredService<IPaymentService>());

        yield return AIFunctionFactory.Create(tools.GetPaymentIntent, name: "finance_get_payment_intent");
        yield return AIFunctionFactory.Create(tools.CreatePaymentIntent, name: "finance_create_payment_intent");
        yield return AIFunctionFactory.Create(tools.CapturePayment, name: "finance_capture_payment");
        yield return AIFunctionFactory.Create(tools.CancelPayment, name: "finance_cancel_payment");
    }
}
