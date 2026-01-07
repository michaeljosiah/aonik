using Aonik.Application.Abstractions.Persistence;
using Aonik.Application.Models.Payments;
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
        var paymentIntent = new PaymentIntent(
            request.Amount,
            request.Currency,
            request.Reference);

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

        paymentIntent.Capture();
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

        paymentIntent.Cancel();
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToResponse(paymentIntent);
    }

    private static PaymentIntentResponse MapToResponse(PaymentIntent paymentIntent)
    {
        return new PaymentIntentResponse(
            paymentIntent.Id,
            paymentIntent.Amount,
            paymentIntent.Currency,
            paymentIntent.Status,
            paymentIntent.Reference,
            paymentIntent.CreatedUtc);
    }
}
