using Aonik.Finance.Contracts.Models.Payments;
using Aonik.Finance.Contracts.Services.Payments;
using Aonik.SharedKernel.Abstractions.Payments;

namespace Aonik.Finance.Services.Integration;

/// <summary>
/// Finance's implementation of the SharedKernel <see cref="IPaymentInitiator"/> write contract
/// (Spec 042 §12). Lets modules that may not reference Finance (e.g. <c>Aonik.Commerce</c>) initiate
/// funding of an order. A thin adapter over <see cref="IPaymentService"/> that creates a
/// <c>PaymentIntent</c> only — capture/settlement remain Finance-governed high-tier actions.
/// </summary>
internal sealed class PaymentInitiator : IPaymentInitiator
{
    private readonly IPaymentService _payments;

    public PaymentInitiator(IPaymentService payments) => _payments = payments;

    public async Task<PaymentIntentRef> CreateIntentForOrderAsync(CreatePaymentIntentForOrderCommand command, CancellationToken cancellationToken = default)
    {
        var request = new CreatePaymentIntentRequest(
            Amount: command.Amount,
            Currency: command.Currency,
            Reference: command.Reference ?? $"order:{command.OrderId:N}",
            OrderId: command.OrderId,
            InvoiceId: command.InvoiceId,
            PayerPartyId: null,
            PaymentMethodType: command.PaymentMethodType);

        var response = await _payments.CreatePaymentIntentAsync(request, cancellationToken);
        return new PaymentIntentRef(response.Id, response.Status.ToString());
    }
}
