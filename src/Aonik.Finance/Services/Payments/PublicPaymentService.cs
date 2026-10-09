using Microsoft.EntityFrameworkCore;

using Aonik.SharedKernel.Abstractions;
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
    private readonly CheckoutPaymentService _checkoutPayments;

    public PublicPaymentService(
        FinanceDbContext dbContext,
        ITenantProvider tenantProvider,
        IEnumerable<IPaymentProviderGateway> providerGateways,
        CheckoutPaymentService checkoutPayments)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _providerGateways = providerGateways;
        _checkoutPayments = checkoutPayments;
    }

    public async Task<GuestPaymentIntentResponse> CreateGuestPaymentIntentAsync(
        CreateGuestPaymentIntentRequest request,
        CancellationToken cancellationToken = default)
    {
        // M7-style defense-in-depth (#221): the guest/checkout surface still relies on an
        // ambient tenant (TenantContextMiddleware resolves it from X-Tenant-Id for anonymous
        // requests before the endpoint's own header guard runs), so the explicit predicate
        // isolates the row independently of the global query filter, not instead of it.
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var order = await _dbContext.Orders
            .FirstOrDefaultAsync(entity => entity.Id == request.OrderId && entity.TenantId == tenantId, cancellationToken);

        if (order == null)
        {
            throw new NotFoundException($"Order {request.OrderId} not found.");
        }

        if (!string.Equals(order.OrderType, "BillPayment", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidStateException("Payment intents can only be created for bill payment orders.");
        }

        if (!string.Equals(order.Status, "Draft", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(order.Status, "PendingFunding", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidStateException("Only draft or pending funding orders can create payment intents.");
        }

        if (order.AmountIn <= 0)
        {
            throw new InvalidStateException("Order amount must be greater than zero to create a payment intent.");
        }

        var provider = ResolveProvider(request.Provider);

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
            // Null when the guest has not yet been resolved to a party — never a Guid.Empty
            // placeholder. The payer is enforced before the intent is authorized.
            PayerPartyId = order.PayerPartyId,
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

    public Task<GuestPaymentIntentResponse> CreateCommerceGuestPaymentIntentAsync(
        CreateCommerceGuestPaymentIntentRequest request,
        CancellationToken cancellationToken = default)
        => _checkoutPayments.CreateAsync(request, cancellationToken);

    public async Task<GuestPaymentIntentStatusResponse?> GetGuestPaymentIntentStatusAsync(
        GetGuestPaymentIntentStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.PaymentIntentId == null && string.IsNullOrWhiteSpace(request.ProviderReference))
        {
            throw new InvalidStateException("Either paymentIntentId or providerReference is required.");
        }

        // Defense-in-depth (#221) — see CreateGuestPaymentIntentAsync.
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var order = await _dbContext.Orders
            .FirstOrDefaultAsync(entity => entity.Id == request.OrderId && entity.TenantId == tenantId, cancellationToken);

        if (order == null)
        {
            return null;
        }

        var query = _dbContext.PaymentIntents
            .Where(entity => entity.OrderId == request.OrderId && entity.TenantId == tenantId);

        if (request.PaymentIntentId != null)
        {
            query = query.Where(entity => entity.Id == request.PaymentIntentId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.ProviderReference))
        {
            var normalizedProviderReference = request.ProviderReference.Trim();
            query = query.Where(entity => entity.ProviderReference == normalizedProviderReference
                || entity.PaymentMethodRef == normalizedProviderReference);
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
            paymentIntent.ProviderReference ?? paymentIntent.PaymentMethodRef ?? string.Empty,
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
            throw new InvalidStateException($"Payment provider '{provider}' is not configured.");
        }

        return gateway;
    }
}
