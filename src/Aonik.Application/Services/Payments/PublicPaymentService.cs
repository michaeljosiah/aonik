using Microsoft.EntityFrameworkCore;

using Aonik.Application.Abstractions.Multitenancy;
using Aonik.Application.Abstractions.Persistence;
using Aonik.Application.Models.Payments;
using Aonik.Domain.Payments.Entities;

namespace Aonik.Application.Services.Payments;

public class PublicPaymentService : IPublicPaymentService
{
    private readonly IAonikDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IEnumerable<IPaymentProviderGateway> _providerGateways;

    public PublicPaymentService(
        IAonikDbContext dbContext,
        ITenantProvider tenantProvider,
        IEnumerable<IPaymentProviderGateway> providerGateways)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _providerGateways = providerGateways;
    }

    public async Task<GuestPaymentIntentResponse> CreateGuestPaymentIntentAsync(
        CreateGuestPaymentIntentRequest request,
        CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Orders
            .FirstOrDefaultAsync(entity => entity.Id == request.OrderId, cancellationToken);

        if (order == null)
        {
            throw new InvalidOperationException($"Order {request.OrderId} not found.");
        }

        if (!string.Equals(order.OrderType, "BillPayment", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Payment intents can only be created for bill payment orders.");
        }

        if (!string.Equals(order.Status, "Draft", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(order.Status, "PendingFunding", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only draft or pending funding orders can create payment intents.");
        }

        if (order.AmountIn <= 0)
        {
            throw new InvalidOperationException("Order amount must be greater than zero to create a payment intent.");
        }

        var provider = ResolveProvider(request.Provider);
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var reference = $"ORD-{order.Id:N}";
        var providerResult = await provider.CreateIntentAsync(
            new PaymentProviderIntentRequest(
                order.Id,
                order.AmountIn,
                order.CurrencyIn,
                request.PaymentMethodType,
                request.ReturnUrl,
                request.CancelUrl,
                reference),
            cancellationToken);

        var paymentIntent = new PaymentIntent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Amount = order.AmountIn,
            Currency = order.CurrencyIn,
            PayerPartyId = order.PayerPartyId ?? Guid.Empty,
            PayeePartyId = null,
            OrderId = order.Id,
            InvoiceId = null,
            PurposeType = "Order",
            PurposeId = order.Id,
            PaymentMethodType = request.PaymentMethodType,
            PaymentMethodRef = providerResult.ProviderReference,
            Status = providerResult.Status
        };

        _dbContext.PaymentIntents.Add(paymentIntent);

        if (string.Equals(order.Status, "Draft", StringComparison.OrdinalIgnoreCase))
        {
            order.Status = "PendingFunding";
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new GuestPaymentIntentResponse(
            paymentIntent.Id,
            paymentIntent.OrderId,
            paymentIntent.Amount,
            paymentIntent.Currency,
            paymentIntent.Status,
            providerResult.Provider,
            providerResult.ProviderReference,
            providerResult.ClientSecret,
            providerResult.CheckoutUrl,
            paymentIntent.CreatedAt);
    }

    private IPaymentProviderGateway ResolveProvider(string provider)
    {
        var normalized = provider.Trim();

        var gateway = _providerGateways.FirstOrDefault(item =>
            string.Equals(item.ProviderCode, normalized, StringComparison.OrdinalIgnoreCase));

        if (gateway == null)
        {
            throw new InvalidOperationException($"Payment provider '{provider}' is not configured.");
        }

        return gateway;
    }
}
