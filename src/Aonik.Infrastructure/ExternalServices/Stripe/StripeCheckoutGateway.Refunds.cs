using OpenTelemetry;
using Stripe;

using Aonik.Finance.Contracts.Services.Payments;

namespace Aonik.Infrastructure.ExternalServices.Stripe;

internal sealed partial class StripeCheckoutGateway
{
    private const int MaximumRefundPages = 10;

    public Task<PaymentProviderRefundSnapshot> CreateRefundAsync(
        PaymentProviderRefundRequest request, CancellationToken cancellationToken = default)
        => WithRefundClientAsync(request, async (client, binding, payment, ct) =>
        {
            var refund = await client.V1.Refunds.CreateAsync(new RefundCreateOptions
            {
                PaymentIntent = request.ProviderPaymentIntentId,
                Amount = checked((long)(request.Amount * 100)),
                Metadata = new Dictionary<string, string>
                {
                    ["tenantId"] = binding.TenantId.ToString("N"),
                    ["connectorId"] = request.ConnectorId.ToString("N"),
                    ["paymentIntentId"] = request.PaymentIntentId.ToString("N"),
                    ["orderId"] = request.OrderId.ToString("N"),
                    ["refundId"] = request.RefundId.ToString("N"),
                },
            }, new RequestOptions { IdempotencyKey = request.IdempotencyKey }, ct);
            return ProjectRefund(binding, payment, request, refund);
        }, cancellationToken);

    public Task<PaymentProviderRefundSnapshot> GetRefundAsync(
        PaymentProviderRefundRequest request, string providerRefundId, CancellationToken cancellationToken = default)
    {
        if (!IsProviderId(providerRefundId, "re_"))
            throw new InvalidOperationException("A bound Stripe refund reference is required.");
        return WithRefundClientAsync(request, async (client, binding, payment, ct) =>
        {
            var refund = await client.V1.Refunds.GetAsync(providerRefundId, cancellationToken: ct);
            if (refund.Id != providerRefundId)
                throw new InvalidOperationException("Stripe returned a different refund.");
            return ProjectRefund(binding, payment, request, refund);
        }, cancellationToken);
    }

    public Task<PaymentProviderRefundSnapshot?> FindRefundAsync(
        PaymentProviderRefundRequest request, CancellationToken cancellationToken = default)
        => WithRefundClientAsync<PaymentProviderRefundSnapshot?>(request, async (client, binding, payment, ct) =>
        {
            var refunds = await ReadRefundsAsync(client, payment, ct);
            var matches = refunds.Where(x => x.Metadata is not null
                && x.Metadata.TryGetValue("refundId", out var value) && value == request.RefundId.ToString("N")).ToList();
            if (matches.Count > 1)
                throw new InvalidOperationException("Stripe refund correlation is ambiguous.");
            // Absence is only an observation. It never authorizes repeating an aged create request.
            return matches.Count == 0 ? null : ProjectRefund(binding, payment, request, matches[0]);
        }, cancellationToken);

    public Task<PaymentProviderRefundBudget> GetRefundBudgetAsync(
        PaymentProviderRefundRequest request, CancellationToken cancellationToken = default)
        => WithRefundClientAsync(request, async (client, binding, payment, ct) =>
        {
            var refunds = await ReadRefundsAsync(client, payment, ct);
            return new PaymentProviderRefundBudget(payment.AmountReceived / 100m, "GBP", refunds.Select(refund =>
                new PaymentProviderRefundObservation(refund.Id, refund.Amount / 100m, "GBP", RefundStatus(refund.Status),
                    CorrelatedRefundId(refund, binding, request))).ToArray());
        }, cancellationToken);

    private async Task<T> WithRefundClientAsync<T>(PaymentProviderRefundRequest request,
        Func<StripeClient, StripeConnectorBinding, PaymentIntent, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.RefundId == Guid.Empty || request.PaymentIntentId == Guid.Empty || request.OrderId == Guid.Empty
            || request.ConnectorId == Guid.Empty || !IsProviderId(request.ProviderPaymentIntentId, "pi_")
            || !IsProviderId(request.ProviderAccountId, "acct_") || request.Currency != "GBP"
            || request.Amount <= 0 || request.Amount > 999999.99m
            || request.Amount * 100 != decimal.Truncate(request.Amount * 100)
            || string.IsNullOrWhiteSpace(request.IdempotencyKey) || request.IdempotencyKey.Length > 255
            || request.IdempotencyKey.Any(char.IsControl))
            throw new InvalidOperationException("Stripe refunds require a bound, keyed GBP payment and an exact positive minor-unit amount.");

        var binding = await connectorResolver.ResolveBoundAsync(request.ConnectorId, cancellationToken);
        ValidateBinding(binding, request.ProviderAccountId, request.LiveMode);
        if (binding.ConnectorId != request.ConnectorId)
            throw new InvalidOperationException("Stripe refund merchant binding has changed.");
        using var suppression = SuppressInstrumentationScope.Begin();
        try
        {
            var client = await CreateVerifiedClientAsync(binding, cancellationToken);
            var payment = await client.V1.PaymentIntents.GetAsync(request.ProviderPaymentIntentId,
                new PaymentIntentGetOptions { Expand = ["latest_charge"] }, cancellationToken: cancellationToken);
            var charge = payment.LatestCharge;
            if (payment.Id != request.ProviderPaymentIntentId || payment.Livemode != binding.LiveMode
                || payment.Status != "succeeded" || payment.Currency != "gbp" || payment.Amount <= 0
                || payment.AmountReceived != payment.Amount || payment.AmountCapturable != 0
                || request.Amount > payment.AmountReceived / 100m
                || MetadataGuid(payment.Metadata, "tenantId") != binding.TenantId
                || MetadataGuid(payment.Metadata, "connectorId") != request.ConnectorId
                || MetadataGuid(payment.Metadata, "paymentIntentId") != request.PaymentIntentId
                || MetadataGuid(payment.Metadata, "orderId") != request.OrderId
                || charge is null || !IsProviderId(charge.Id, "ch_") || charge.Id != payment.LatestChargeId
                || charge.PaymentIntentId != payment.Id || charge.Livemode != binding.LiveMode
                || !charge.Paid || !charge.Captured || charge.Amount != payment.Amount || charge.Currency != "gbp")
                throw new InvalidOperationException("Stripe refund funding does not match its original captured payment.");
            return await operation(client, binding, payment, cancellationToken);
        }
        catch (Exception ex) when (ex is StripeException or HttpRequestException
            || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            // A transport/provider error does not prove that an external refund failed.
            throw new InvalidOperationException("Stripe refund state could not be confirmed.");
        }
    }

    private static async Task<IReadOnlyList<Refund>> ReadRefundsAsync(
        StripeClient client, PaymentIntent payment, CancellationToken cancellationToken)
    {
        var refunds = new List<Refund>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        string? cursor = null;
        for (var page = 0; page < MaximumRefundPages; page++)
        {
            var batch = await client.V1.Refunds.ListAsync(new RefundListOptions
            {
                PaymentIntent = payment.Id, Limit = 100, StartingAfter = cursor,
            }, cancellationToken: cancellationToken);
            if (batch.Data is null || batch.Data.Count > 100 || (batch.HasMore && batch.Data.Count == 0))
                throw new InvalidOperationException("Stripe refund history could not be completely verified.");
            foreach (var refund in batch.Data)
            {
                ValidateRefundPayment(payment, refund);
                if (!ids.Add(refund.Id))
                    throw new InvalidOperationException("Stripe refund history contains duplicate references.");
                refunds.Add(refund);
            }
            if (!batch.HasMore) return refunds;
            cursor = batch.Data[^1].Id;
        }
        throw new InvalidOperationException("Stripe refund history exceeds the reconciliation limit.");
    }

    private static PaymentProviderRefundSnapshot ProjectRefund(StripeConnectorBinding binding, PaymentIntent payment,
        PaymentProviderRefundRequest request, Refund refund)
    {
        ValidateRefundPayment(payment, refund);
        if (refund.Amount / 100m != request.Amount || CorrelatedRefundId(refund, binding, request) != request.RefundId)
            throw new InvalidOperationException("Stripe refund evidence does not match the authorized return.");
        return new PaymentProviderRefundSnapshot(binding.TenantId, request.RefundId, request.PaymentIntentId,
            request.OrderId, binding.ConnectorId, binding.ProviderAccountId, binding.LiveMode, refund.Id,
            payment.Id, refund.ChargeId, refund.Amount / 100m, "GBP", RefundStatus(refund.Status), refund.Created,
            SafeProviderCode(refund.FailureReason), SafeProviderCode(refund.BalanceTransactionId),
            SafeProviderCode(refund.FailureBalanceTransactionId));
    }

    private static void ValidateRefundPayment(PaymentIntent payment, Refund refund)
    {
        if (!IsProviderId(refund.Id, "re_") || refund.PaymentIntentId != payment.Id
            || refund.ChargeId != payment.LatestChargeId || refund.Currency != "gbp"
            || refund.Amount <= 0 || refund.Amount > payment.AmountReceived)
            throw new InvalidOperationException("Stripe refund does not match its original payment.");
    }

    private static Guid? CorrelatedRefundId(Refund refund, StripeConnectorBinding binding, PaymentProviderRefundRequest request)
    {
        var metadata = refund.Metadata;
        if (metadata is null || !metadata.TryGetValue("refundId", out var value)
            || !Guid.TryParseExact(value, "N", out var refundId) || refundId == Guid.Empty
            || !metadata.TryGetValue("tenantId", out var tenant) || tenant != binding.TenantId.ToString("N")
            || !metadata.TryGetValue("connectorId", out var connector) || connector != request.ConnectorId.ToString("N")
            || !metadata.TryGetValue("paymentIntentId", out var intent) || intent != request.PaymentIntentId.ToString("N")
            || !metadata.TryGetValue("orderId", out var order) || order != request.OrderId.ToString("N")) return null;
        return refundId;
    }

    private static bool IsProviderId(string? value, string prefix)
        => value is not null && value.Length > prefix.Length && value.Length <= 200
            && value.StartsWith(prefix, StringComparison.Ordinal) && value.All(c => char.IsAsciiLetterOrDigit(c) || c == '_');

    private static string RefundStatus(string? status)
        => status is "pending" or "requires_action" or "succeeded" or "failed" or "canceled" ? status : "unknown";

    private static string? SafeProviderCode(string? value)
        => value is not null && value.Length is > 0 and <= 200
            && value.All(c => char.IsAsciiLetterOrDigit(c) || c == '_') ? value : null;
}
