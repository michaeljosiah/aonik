using Microsoft.EntityFrameworkCore;

using Aonik.Application.Abstractions.Multitenancy;
using Aonik.Application.Abstractions.Persistence;
using Aonik.Application.Models.Payments;
using Aonik.Application.Services;
using Aonik.Application.Services.Identity;
using Aonik.Domain.Payments;
using Aonik.Domain.Payments.Entities;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Application.Services.Payments;

public class PaymentService : AdminServiceBase, IPaymentService
{
    private readonly IAonikDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;

    public PaymentService(
        IAonikDbContext dbContext,
        ITenantProvider tenantProvider,
        IPermissionService permissionService,
        ICurrentUserProvider currentUserProvider)
        : base(currentUserProvider, permissionService)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
    }

    public async Task<PaymentIntentResponse> CreatePaymentIntentAsync(CreatePaymentIntentRequest request, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Payment.Create", cancellationToken);
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var paymentIntent = new PaymentIntent
        {
            Id = Guid.NewGuid(),
            Amount = request.Amount,
            Currency = request.Currency,
            Status = PaymentStatus.Pending.ToString(),
            PurposeType = "Order",
            PurposeId = request.OrderId,
            PayerPartyId = Guid.Empty, // TODO: Add to request or get from context
            PayeePartyId = null,
            OrderId = request.OrderId,
            InvoiceId = request.InvoiceId,
            PaymentMethodType = "Card", // TODO: Add to request
            PaymentMethodRef = request.Reference,
            TenantId = tenantId
        };

        _dbContext.PaymentIntents.Add(paymentIntent);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToResponse(paymentIntent);
    }

    public async Task<PaymentIntentResponse?> GetPaymentIntentAsync(Guid paymentIntentId, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Payment.Read", cancellationToken);
        var paymentIntent = await _dbContext.PaymentIntents
            .FirstOrDefaultAsync(p => p.Id == paymentIntentId, cancellationToken);

        return paymentIntent == null ? null : MapToResponse(paymentIntent);
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

        paymentIntent.Status = PaymentStatus.Captured.ToString();
        await _dbContext.SaveChangesAsync(cancellationToken);

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
