using Microsoft.EntityFrameworkCore;

using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions;
using Aonik.Finance.Contracts.Models.Payments;
using Aonik.Finance.Contracts.Services.Payments;
using Aonik.Finance.Entities.Payments;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.Ledger;
using Aonik.Finance.Services.Observability;

namespace Aonik.Finance.Services.Payments;

internal class PaymentService : FinanceServiceBase, IPaymentService
{
    private readonly FinanceDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly FinanceMetrics _metrics;
    private readonly LedgerPostingService _ledgerPoster;

    public PaymentService(
        FinanceDbContext dbContext,
        ITenantProvider tenantProvider,
        IPermissionService permissionService,
        ICurrentUserProvider currentUserProvider,
        FinanceMetrics metrics,
        LedgerPostingService ledgerPoster)
        : base(currentUserProvider, permissionService)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _metrics = metrics;
        _ledgerPoster = ledgerPoster;
    }

    public async Task<PaymentIntentResponse> CreatePaymentIntentAsync(CreatePaymentIntentRequest request, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Payment.Create", cancellationToken);
        var tenantId = _tenantProvider.GetCurrentTenantId();

        // An intent always funds an order, and the order is the canonical record of who
        // is paying. Resolve the payer from the order (or an explicit override) instead
        // of persisting a placeholder; loading the order also stops dangling intents that
        // reference a non-existent order.
        var order = await _dbContext.Orders
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);

        if (order == null)
        {
            throw new InvalidOperationException($"Order with ID {request.OrderId} not found");
        }

        // Null (never Guid.Empty / a fabricated "Card") models genuine absence on a draft;
        // both payer and method are enforced at the authorize money-movement boundary.
        var payerPartyId = request.PayerPartyId ?? order.PayerPartyId;
        var paymentMethodType = string.IsNullOrWhiteSpace(request.PaymentMethodType)
            ? null
            : request.PaymentMethodType.Trim();

        var paymentIntent = new PaymentIntent
        {
            Id = Guid.NewGuid(),
            Amount = request.Amount,
            Currency = request.Currency,
            Status = PaymentStatus.Pending.ToString(),
            PurposeType = "Order",
            PurposeId = request.OrderId,
            PayerPartyId = payerPartyId,
            PayeePartyId = null,
            OrderId = request.OrderId,
            InvoiceId = request.InvoiceId,
            PaymentMethodType = paymentMethodType,
            PaymentMethodRef = request.Reference,
            TenantId = tenantId
        };

        _dbContext.PaymentIntents.Add(paymentIntent);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Per-tenant payment metric. Status starts at "Pending" here; the
        // capture / cancel paths emit additional events with their own
        // status so a dashboard can chart pending → captured / cancelled.
        _metrics.RecordPayment(tenantId, paymentIntent.Currency, paymentIntent.Status);

        return MapToResponse(paymentIntent);
    }

    public async Task<PaymentIntentResponse?> GetPaymentIntentAsync(Guid paymentIntentId, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Payment.Read", cancellationToken);
        var paymentIntent = await _dbContext.PaymentIntents
            .FirstOrDefaultAsync(p => p.Id == paymentIntentId, cancellationToken);

        return paymentIntent == null ? null : MapToResponse(paymentIntent);
    }

    public async Task<PaymentIntentResponse> AuthorizePaymentAsync(Guid paymentIntentId, CancellationToken cancellationToken = default)
    {
        // Authorize is the first leg of the capture flow, so it is gated by the
        // same Payment.Capture permission rather than a separate (unseeded) one.
        await EnsurePermissionAsync("Payment.Capture", cancellationToken);
        var paymentIntent = await _dbContext.PaymentIntents
            .FirstOrDefaultAsync(p => p.Id == paymentIntentId, cancellationToken);

        if (paymentIntent == null)
        {
            throw new InvalidOperationException($"Payment intent with ID {paymentIntentId} not found");
        }

        // Business logic: only a pending intent can be authorized.
        if (!Enum.TryParse<PaymentStatus>(paymentIntent.Status, out var currentStatus))
        {
            throw new InvalidOperationException($"Invalid payment status: {paymentIntent.Status}");
        }

        if (currentStatus != PaymentStatus.Pending)
        {
            throw new InvalidOperationException("Only pending payments can be authorized");
        }

        // Externally material boundary (Spec / issue #104): money must not be authorized to
        // move on behalf of an unknown payer or via an unspecified rail. Fail closed — these
        // are unset only on drafts that never resolved a payer/method, and the Guid.Empty
        // check also rejects any legacy rows persisted before the column became nullable.
        if (paymentIntent.PayerPartyId is null || paymentIntent.PayerPartyId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Cannot authorize payment: the intent has no resolved payer.");
        }

        if (string.IsNullOrWhiteSpace(paymentIntent.PaymentMethodType))
        {
            throw new InvalidOperationException(
                "Cannot authorize payment: the intent has no payment method.");
        }

        paymentIntent.Status = PaymentStatus.Authorized.ToString();
        await _dbContext.SaveChangesAsync(cancellationToken);

        _metrics.RecordPayment(paymentIntent.TenantId, paymentIntent.Currency, paymentIntent.Status);

        return MapToResponse(paymentIntent);
    }

    public async Task<PaymentIntentResponse> CapturePaymentAsync(Guid paymentIntentId, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Payment.Capture", cancellationToken);
        var paymentIntent = await _dbContext.PaymentIntents
            .FirstOrDefaultAsync(p => p.Id == paymentIntentId, cancellationToken);

        if (paymentIntent == null)
        {
            throw new InvalidOperationException($"Payment intent with ID {paymentIntentId} not found");
        }

        // Business logic: Validate current status before capturing
        if (!Enum.TryParse<PaymentStatus>(paymentIntent.Status, out var currentStatus))
        {
            throw new InvalidOperationException($"Invalid payment status: {paymentIntent.Status}");
        }

        if (currentStatus != PaymentStatus.Authorized)
        {
            throw new InvalidOperationException("Only authorized payments can be captured");
        }

        // Record the cash receipt in the ledger BEFORE flipping the status:
        // Dr Cash / Cr Payments Clearing. If the post fails the intent stays
        // Authorized and the capture can be retried; the post is idempotent per
        // payment intent so a retry after a partial success cannot double-post.
        await _ledgerPoster.PostPaymentCaptureAsync(paymentIntent, cancellationToken);

        paymentIntent.Status = PaymentStatus.Captured.ToString();
        await _dbContext.SaveChangesAsync(cancellationToken);

        _metrics.RecordPayment(paymentIntent.TenantId, paymentIntent.Currency, paymentIntent.Status);

        return MapToResponse(paymentIntent);
    }

    public async Task<PaymentIntentResponse> CancelPaymentAsync(Guid paymentIntentId, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Payment.Cancel", cancellationToken);
        var paymentIntent = await _dbContext.PaymentIntents
            .FirstOrDefaultAsync(p => p.Id == paymentIntentId, cancellationToken);

        if (paymentIntent == null)
        {
            throw new InvalidOperationException($"Payment intent with ID {paymentIntentId} not found");
        }

        // Business logic: Validate current status before cancelling
        if (!Enum.TryParse<PaymentStatus>(paymentIntent.Status, out var currentStatus))
        {
            throw new InvalidOperationException($"Invalid payment status: {paymentIntent.Status}");
        }

        if (currentStatus == PaymentStatus.Captured)
        {
            throw new InvalidOperationException("Captured payments cannot be cancelled");
        }

        paymentIntent.Status = PaymentStatus.Cancelled.ToString();
        await _dbContext.SaveChangesAsync(cancellationToken);

        _metrics.RecordPayment(paymentIntent.TenantId, paymentIntent.Currency, paymentIntent.Status);

        return MapToResponse(paymentIntent);
    }

    private static PaymentIntentResponse MapToResponse(PaymentIntent paymentIntent)
    {
        // Parse status string to enum, default to Pending if invalid
        if (!Enum.TryParse<PaymentStatus>(paymentIntent.Status, out var status))
        {
            status = PaymentStatus.Pending;
        }

        return new PaymentIntentResponse(
            paymentIntent.Id,
            paymentIntent.OrderId,
            paymentIntent.InvoiceId,
            paymentIntent.Amount,
            paymentIntent.Currency,
            status,
            paymentIntent.PaymentMethodRef ?? string.Empty,
            paymentIntent.CreatedAt);
    }
}
