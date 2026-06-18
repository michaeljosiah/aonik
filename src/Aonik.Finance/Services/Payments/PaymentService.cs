using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Events.Integration;
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
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        FinanceDbContext dbContext,
        ITenantProvider tenantProvider,
        IPermissionService permissionService,
        ICurrentUserProvider currentUserProvider,
        FinanceMetrics metrics,
        LedgerPostingService ledgerPoster,
        ILogger<PaymentService> logger)
        : base(currentUserProvider, permissionService)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _metrics = metrics;
        _ledgerPoster = ledgerPoster;
        _logger = logger;
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
            throw new NotFoundException($"Order with ID {request.OrderId} not found");
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
            throw new NotFoundException($"Payment intent with ID {paymentIntentId} not found");
        }

        // Business logic: only a pending intent can be authorized.
        if (!Enum.TryParse<PaymentStatus>(paymentIntent.Status, out var currentStatus))
        {
            throw new InvalidStateException($"Invalid payment status: {paymentIntent.Status}");
        }

        if (currentStatus != PaymentStatus.Pending)
        {
            throw new InvalidStateException("Only pending payments can be authorized");
        }

        // Externally material boundary (issue #104): money must not be authorized to move on
        // behalf of an unknown payer or via an unspecified rail.
        EnsurePayerAndMethodResolved(paymentIntent, "authorize");

        paymentIntent.Status = PaymentStatus.Authorized.ToString();
        await _dbContext.SaveChangesAsync(cancellationToken);

        _metrics.RecordPayment(paymentIntent.TenantId, paymentIntent.Currency, paymentIntent.Status);

        return MapToResponse(paymentIntent);
    }

    public async Task<PaymentIntentResponse> CapturePaymentAsync(Guid paymentIntentId, CancellationToken cancellationToken = default)
    {
        // Observability for Issue #142. Span + scope are opened BEFORE the
        // permission check so an unauthorized capture attempt is still
        // traceable. OrderId is resolved from the intent once loaded; until
        // then the scope only carries PaymentIntentId.
        using var activity = FinanceActivitySource.Source.StartActivity("payment.capture");
        activity?.SetTag(FinanceActivitySource.StageTag, MoneyActionStages.Capture);
        activity?.SetTag(FinanceActivitySource.PaymentIntentIdTag, paymentIntentId);

        Guid resolvedOrderId = Guid.Empty;
        Guid resolvedTenantId = Guid.Empty;

        try
        {
            await EnsurePermissionAsync("Payment.Capture", cancellationToken);
            var paymentIntent = await _dbContext.PaymentIntents
                .FirstOrDefaultAsync(p => p.Id == paymentIntentId, cancellationToken);

            if (paymentIntent == null)
            {
                throw new NotFoundException($"Payment intent with ID {paymentIntentId} not found");
            }

            resolvedOrderId = paymentIntent.OrderId;
            resolvedTenantId = paymentIntent.TenantId;
            activity?.SetTag(FinanceActivitySource.OrderIdTag, resolvedOrderId);
            activity?.SetTag(FinanceActivitySource.TenantIdTag, resolvedTenantId);

            // BeginOrderScope now that we know the OrderId. Every child log
            // (ledger posting, EF SaveChanges) inherits OrderId + PaymentIntentId
            // so a KQL pivot on either id returns the full capture trace.
            using var orderScope = _logger.BeginOrderScope(resolvedOrderId, paymentIntentId: paymentIntentId);

            // Business logic: Validate current status before capturing
            if (!Enum.TryParse<PaymentStatus>(paymentIntent.Status, out var currentStatus))
            {
                throw new InvalidStateException($"Invalid payment status: {paymentIntent.Status}");
            }

            if (currentStatus != PaymentStatus.Authorized)
            {
                throw new InvalidStateException("Only authorized payments can be captured");
            }

            // Re-enforce the externally material boundary here, not only at authorize: capture is
            // the step that actually posts to the ledger, and an intent can be Authorized without
            // ever passing through AuthorizePaymentAsync (legacy rows created before this invariant
            // existed, with a Guid.Empty/blank payer or rail). Fail closed before any money moves.
            EnsurePayerAndMethodResolved(paymentIntent, "capture");

            // Record the cash receipt in the ledger BEFORE flipping the status:
            // Dr Cash / Cr Payments Clearing. If the post fails the intent stays
            // Authorized and the capture can be retried; the post is idempotent per
            // payment intent so a retry after a partial success cannot double-post.
            await _ledgerPoster.PostPaymentCaptureAsync(paymentIntent, cancellationToken);

            paymentIntent.Status = PaymentStatus.Captured.ToString();

            // Publish payment completion in the same transaction as the status flip + ledger post
            // (transactional outbox). Downstream modules react to this — e.g. Aonik.Commerce commits
            // reserved stock, closes the cart, and completes the ProductPurchase order. Capture is the
            // single point where an intent becomes Captured, so this is the one producer.
            _dbContext.EnqueueIntegrationEvent(new PaymentCompletedEvent(
                paymentIntent.TenantId,
                paymentIntent.Id,
                paymentIntent.OrderId,
                paymentIntent.Amount,
                paymentIntent.Currency));

            await _dbContext.SaveChangesAsync(cancellationToken);

            _metrics.RecordPayment(paymentIntent.TenantId, paymentIntent.Currency, paymentIntent.Status);

            activity?.SetTag(FinanceActivitySource.OutcomeTag, MoneyActionOutcomes.Success);
            _logger.PaymentCaptured(
                resolvedOrderId,
                resolvedTenantId,
                paymentIntentId,
                paymentIntent.Amount,
                paymentIntent.Currency);

            return MapToResponse(paymentIntent);
        }
        catch (Exception ex)
        {
            activity?.SetTag(FinanceActivitySource.OutcomeTag, MoneyActionOutcomes.Failed);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.PaymentCaptureFailed(
                resolvedOrderId,
                resolvedTenantId,
                paymentIntentId,
                ex.Message,
                ex);
            throw;
        }
    }

    public async Task<PaymentIntentResponse> CancelPaymentAsync(Guid paymentIntentId, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Payment.Cancel", cancellationToken);
        var paymentIntent = await _dbContext.PaymentIntents
            .FirstOrDefaultAsync(p => p.Id == paymentIntentId, cancellationToken);

        if (paymentIntent == null)
        {
            throw new NotFoundException($"Payment intent with ID {paymentIntentId} not found");
        }

        // Business logic: Validate current status before cancelling
        if (!Enum.TryParse<PaymentStatus>(paymentIntent.Status, out var currentStatus))
        {
            throw new InvalidStateException($"Invalid payment status: {paymentIntent.Status}");
        }

        if (currentStatus == PaymentStatus.Captured)
        {
            throw new InvalidStateException("Captured payments cannot be cancelled");
        }

        paymentIntent.Status = PaymentStatus.Cancelled.ToString();
        await _dbContext.SaveChangesAsync(cancellationToken);

        _metrics.RecordPayment(paymentIntent.TenantId, paymentIntent.Currency, paymentIntent.Status);

        return MapToResponse(paymentIntent);
    }

    // Externally material guard (issue #104): an intent must have a real payer and a concrete
    // rail before money can move. Enforced at BOTH authorize and capture so a legacy intent that
    // is already Authorized (Guid.Empty payer / blank method) cannot be captured and post to the
    // ledger. The Guid.Empty check also rejects rows persisted before the column became nullable.
    private static void EnsurePayerAndMethodResolved(PaymentIntent paymentIntent, string action)
    {
        if (paymentIntent.PayerPartyId is null || paymentIntent.PayerPartyId == Guid.Empty)
        {
            throw new InvalidStateException(
                $"Cannot {action} payment: the intent has no resolved payer.");
        }

        if (string.IsNullOrWhiteSpace(paymentIntent.PaymentMethodType))
        {
            throw new InvalidStateException(
                $"Cannot {action} payment: the intent has no payment method.");
        }
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
