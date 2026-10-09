using Aonik.SharedKernel.Abstractions.Payments;

namespace Aonik.Application.Tests.Commerce;

internal abstract class TestPaymentState : IPaymentInitiator
{
    public Dictionary<Guid, PaymentIntentStateRef> States { get; } = [];
    public abstract Task<PaymentIntentRef> CreateGuestIntentForOrderAsync(CreateGuestPaymentIntentForOrderCommand command,
        CancellationToken ct = default);

    protected PaymentIntentRef Record(CreateGuestPaymentIntentForOrderCommand command, string secret, string url)
    {
        var id = command.PaymentIntentId!.Value;
        States[id] = new(id, command.OrderId, command.Amount, command.Currency, "Pending", false, url);
        return new(id, "Pending", secret, url);
    }

    public Task<PaymentIntentStateRef?> GetStateAsync(Guid paymentIntentId, CancellationToken cancellationToken = default)
        => Task.FromResult(States.GetValueOrDefault(paymentIntentId));

    public Task<PaymentIntentStateRef> ExpireAsync(Guid paymentIntentId, CancellationToken cancellationToken = default)
    {
        var state = States[paymentIntentId];
        if (state.Status is not ("Captured" or "Processing"))
            state = state with { Status = "Cancelled", CanNoLongerPay = true, CheckoutUrl = null };
        States[paymentIntentId] = state;
        return Task.FromResult(state);
    }
}
