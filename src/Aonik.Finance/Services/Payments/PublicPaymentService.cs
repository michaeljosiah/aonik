using Microsoft.EntityFrameworkCore;

using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Finance.Contracts.Models.Payments;
using Aonik.Finance.Contracts.Services.Payments;
using Aonik.Finance.Entities.Payments;
using Aonik.Finance.Persistence;

namespace Aonik.Finance.Services.Payments;

internal class PublicPaymentService : IPublicPaymentService
{
    private readonly FinanceDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IEnumerable<IPaymentProviderGateway> _providerGateways;

    public PublicPaymentService(
        FinanceDbContext dbContext,
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



    public async Task<GuestPaymentIntentStatusResponse?> GetGuestPaymentIntentStatusAsync(
        GetGuestPaymentIntentStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.PaymentIntentId == null && string.IsNullOrWhiteSpace(request.ProviderReference))
        {
            throw new InvalidOperationException("Either paymentIntentId or providerReference is required.");
        }

        var order = await _dbContext.Orders
            .FirstOrDefaultAsync(entity => entity.Id == request.OrderId, cancellationToken);

        if (order == null)
        {
            return null;
        }

        var query = _dbContext.PaymentIntents
            .Where(entity => entity.OrderId == request.OrderId);

        if (request.PaymentIntentId != null)
        {
            query = query.Where(entity => entity.Id == request.PaymentIntentId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.ProviderReference))
        {
            var normalizedProviderReference = request.ProviderReference.Trim();
            query = query.Where(entity => entity.PaymentMethodRef == normalizedProviderReference);
        }

        var paymentIntent = await query
            .OrderByDescending(entity => entity.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (paymentIntent == null)
        {
            return null;
        }

        return new GuestPaymentIntentStatusResponse(
            paymentIntent.Id,
            paymentIntent.OrderId,
            paymentIntent.Amount,
            paymentIntent.Currency,
            paymentIntent.Status,
            paymentIntent.PaymentMethodRef ?? string.Empty,
            paymentIntent.CreatedAt,
            order.Status);
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
