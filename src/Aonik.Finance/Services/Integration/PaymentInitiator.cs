using Aonik.Finance.Contracts.Models.Payments;
using Aonik.Finance.Contracts.Services.Payments;
using Aonik.SharedKernel.Abstractions.Payments;

namespace Aonik.Finance.Services.Integration;

/// <summary>
/// Finance's implementation of the SharedKernel <see cref="IPaymentInitiator"/> write contract
/// (Spec 042 §12). Lets modules that may not reference Finance (e.g. <c>Aonik.Commerce</c>) initiate
/// funding of an order through the permission-free <see cref="IPublicPaymentService"/> guest path, so
/// anonymous storefront checkout works. Creates a <c>PaymentIntent</c> only — capture/settlement
/// remain Finance-governed high-tier actions.
/// </summary>
internal sealed class PaymentInitiator : IPaymentInitiator
{
    private readonly IPublicPaymentService _publicPayments;

    public PaymentInitiator(IPublicPaymentService publicPayments) => _publicPayments = publicPayments;

    public async Task<PaymentIntentRef> CreateGuestIntentForOrderAsync(CreateGuestPaymentIntentForOrderCommand command, CancellationToken cancellationToken = default)
    {
        var response = await _publicPayments.CreateCommerceGuestPaymentIntentAsync(
            new CreateCommerceGuestPaymentIntentRequest(
                OrderId: command.OrderId,
                Amount: command.Amount,
                Currency: command.Currency,
                Provider: command.Provider,
                PaymentMethodType: command.PaymentMethodType,
                ReturnUrl: command.ReturnUrl,
                CancelUrl: command.CancelUrl),
            cancellationToken);

        return new PaymentIntentRef(response.PaymentIntentId, response.Status, response.ClientSecret, response.CheckoutUrl);
    }
}
