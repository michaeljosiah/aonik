using Aonik.Application.Abstractions.Persistence;
using Aonik.Application.Models.Payments;
using Aonik.Domain.Payments;
using Aonik.Domain.Payments.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Services.Payments;

public class PaymentService : IPaymentService
{
    private readonly IAonikDbContext _dbContext;

    public PaymentService(IAonikDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PaymentIntentResponse> CreatePaymentIntentAsync(CreatePaymentIntentRequest request, CancellationToken cancellationToken = default)
    {
        var paymentIntent = new PaymentIntent
        {
            Id = Guid.NewGuid(),
            Amount = request.Amount,
            Currency = request.Currency,
            Status = PaymentStatus.Pending.ToString(),
            PurposeType = "Invoice", // TODO: Add to request or determine from context
            PurposeId = Guid.Empty, // TODO: Add to request
            PayerPartyId = Guid.Empty, // TODO: Add to request or get from context
            PayeePartyId = null,
            PaymentMethodType = "Card", // TODO: Add to request
            PaymentMethodRef = request.Reference,
            TenantId = Guid.Empty // TODO: Get from ITenantProvider or context
        };

        _dbContext.PaymentIntents.Add(paymentIntent);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToResponse(paymentIntent);
    }

    public async Task<PaymentIntentResponse?> GetPaymentIntentAsync(Guid paymentIntentId, CancellationToken cancellationToken = default)
    {
        var paymentIntent = await _dbContext.PaymentIntents
            .FirstOrDefaultAsync(p => p.Id == paymentIntentId, cancellationToken);

        return paymentIntent == null ? null : MapToResponse(paymentIntent);
    }

    public async Task<PaymentIntentResponse> CapturePaymentAsync(Guid paymentIntentId, CancellationToken cancellationToken = default)
    {
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
            paymentIntent.Amount,
            paymentIntent.Currency,
            status,
            paymentIntent.PaymentMethodRef ?? string.Empty,
            paymentIntent.CreatedAt);
    }
}
