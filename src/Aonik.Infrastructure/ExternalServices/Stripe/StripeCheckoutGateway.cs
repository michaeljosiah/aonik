using OpenTelemetry;
using Stripe;
using Stripe.Checkout;

using Aonik.Finance.Contracts.Services.Payments;
using Aonik.SharedKernel.Abstractions.Multitenancy;

namespace Aonik.Infrastructure.ExternalServices.Stripe;

internal sealed class StripeCheckoutGateway(
    HttpClient httpClient,
    IStripeConnectorResolver connectorResolver,
    ITenantProvider tenantProvider) : IPaymentProviderGateway
{
    internal static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);
    internal const long MaxResponseBytes = 1024 * 1024;
    public string ProviderCode => "Stripe";

    public async Task<PaymentProviderIntentResult> CreateIntentAsync(
        PaymentProviderIntentRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.PaymentIntentId == Guid.Empty || request.OrderId == Guid.Empty || request.ConnectorId is null
            || request.LiveMode is null || string.IsNullOrWhiteSpace(request.ProviderAccountId)
            || string.IsNullOrWhiteSpace(request.IdempotencyKey) || request.IdempotencyKey.Length > 255
            || request.IdempotencyKey.Any(char.IsControl)
            || !string.Equals(request.Currency, "GBP", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(request.PaymentMethodType, "Card", StringComparison.OrdinalIgnoreCase)
            || request.Amount < 0.30m || request.Amount > 999999.99m
            || request.Amount * 100 != decimal.Truncate(request.Amount * 100))
        {
            throw new InvalidOperationException("Stripe requires a bound, keyed GBP card checkout with an exact positive minor-unit amount.");
        }

        var binding = await connectorResolver.ResolveBoundAsync(request.ConnectorId.Value, cancellationToken);
        ValidateBinding(binding, request.ProviderAccountId, request.LiveMode.Value);
        ValidateReturnUrl(request.ReturnUrl, binding.ReturnOrigin);
        ValidateReturnUrl(request.CancelUrl, binding.ReturnOrigin);
        using var suppression = SuppressInstrumentationScope.Begin();
        var client = await CreateVerifiedClientAsync(binding, cancellationToken);
        var metadata = new Dictionary<string, string>
        {
            ["tenantId"] = binding.TenantId.ToString("N"),
            ["connectorId"] = binding.ConnectorId.ToString("N"),
            ["paymentIntentId"] = request.PaymentIntentId.ToString("N"),
            ["orderId"] = request.OrderId.ToString("N"),
        };
        var options = new SessionCreateOptions
        {
            Mode = "payment", UiMode = "hosted_page", AllowedPaymentMethodTypes = ["card"],
            SuccessUrl = request.ReturnUrl, CancelUrl = request.CancelUrl,
            ClientReferenceId = request.PaymentIntentId.ToString("N"), Metadata = metadata,
            PaymentIntentData = new SessionPaymentIntentDataOptions { Metadata = metadata, CaptureMethod = "automatic" },
            AdaptivePricing = new SessionAdaptivePricingOptions { Enabled = false },
            AfterExpiration = new SessionAfterExpirationOptions
            {
                Recovery = new SessionAfterExpirationRecoveryOptions { Enabled = false },
            },
            LineItems = [new SessionLineItemOptions
            {
                Quantity = 1,
                PriceData = new SessionLineItemPriceDataOptions
                {
                    Currency = "gbp", UnitAmount = checked((long)(request.Amount * 100)),
                    ProductData = new SessionLineItemPriceDataProductDataOptions { Name = "Order payment" },
                },
            }],
            Expand = ["payment_intent.latest_charge"],
        };

        Session session;
        try
        {
            session = await client.V1.Checkout.Sessions.CreateAsync(options,
                new RequestOptions { IdempotencyKey = request.IdempotencyKey }, cancellationToken);
        }
        catch (StripeException)
        {
            // Provider exceptions may contain request details; the durable claim remains unresolved.
            throw new InvalidOperationException("Stripe checkout creation could not be confirmed.");
        }

        PaymentProviderCheckoutSnapshot snapshot;
        try
        {
            snapshot = await ProjectAsync(client, binding, session, null, cancellationToken);
        }
        catch (StripeException)
        {
            throw new InvalidOperationException("Stripe checkout state could not be confirmed.");
        }
        if (snapshot.PaymentIntentId != request.PaymentIntentId || snapshot.OrderId != request.OrderId
            || snapshot.Amount != request.Amount)
        {
            throw new InvalidOperationException("Stripe checkout does not match the bound request.");
        }

        return new PaymentProviderIntentResult(ProviderCode, session.Id, snapshot.Status, null, snapshot.CheckoutUrl, snapshot);
    }

    public Task<PaymentProviderSetupIntentResult> CreateSetupIntentAsync(
        PaymentProviderSetupIntentRequest request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This Stripe connector supports one-off hosted checkout only.");

    public Task<PaymentProviderCheckoutSnapshot> GetCheckoutAsync(
        PaymentProviderCheckoutReference reference, CancellationToken cancellationToken = default)
        => ReadAsync(reference, expire: false, cancellationToken);

    public Task<PaymentProviderCheckoutSnapshot> ExpireCheckoutAsync(
        PaymentProviderCheckoutReference reference, CancellationToken cancellationToken = default)
        => ReadAsync(reference, expire: true, cancellationToken);

    private async Task<PaymentProviderCheckoutSnapshot> ReadAsync(
        PaymentProviderCheckoutReference reference, bool expire, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(reference.SessionId) || !reference.SessionId.StartsWith("cs_", StringComparison.Ordinal)
            || reference.SessionId.Length > 255 || reference.SessionId.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '_'))
        {
            throw new InvalidOperationException("A bound Stripe Checkout Session is required.");
        }

        var binding = await connectorResolver.ResolveBoundAsync(reference.ConnectorId, cancellationToken);
        ValidateBinding(binding, reference.ProviderAccountId, reference.LiveMode);
        using var suppression = SuppressInstrumentationScope.Begin();
        var client = await CreateVerifiedClientAsync(binding, cancellationToken);
        try
        {
            if (expire)
            {
                try
                {
                    await client.V1.Checkout.Sessions.ExpireAsync(reference.SessionId, cancellationToken: cancellationToken);
                }
                catch (StripeException)
                {
                    // Already expired, success race, or ambiguous response: only the fresh read below decides.
                }
            }

            var session = await client.V1.Checkout.Sessions.GetAsync(reference.SessionId,
                new SessionGetOptions { Expand = ["payment_intent.latest_charge"] }, cancellationToken: cancellationToken);
            if (session.Id != reference.SessionId)
            {
                throw new InvalidOperationException("Stripe returned a different Checkout Session.");
            }

            return await ProjectAsync(client, binding, session, reference.ProviderPaymentIntentId, cancellationToken);
        }
        catch (StripeException)
        {
            throw new InvalidOperationException("Stripe checkout state could not be confirmed.");
        }
    }

    private void ValidateBinding(StripeConnectorBinding binding, string accountId, bool liveMode)
    {
        if (binding.TenantId != tenantProvider.GetCurrentTenantId() || binding.ProviderAccountId != accountId || binding.LiveMode != liveMode)
        {
            throw new InvalidOperationException("Stripe checkout merchant binding has changed.");
        }
    }

    private async Task<StripeClient> CreateVerifiedClientAsync(StripeConnectorBinding binding, CancellationToken cancellationToken)
    {
        var client = new StripeClient(binding.SecretKey,
            httpClient: new SystemNetHttpClient(httpClient, maxNetworkRetries: 0, enableTelemetry: false));
        try
        {
            var account = await client.V1.Accounts.GetSelfAsync(cancellationToken: cancellationToken);
            if (!string.Equals(account.Id, binding.ProviderAccountId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Stripe API credentials do not belong to the configured merchant.");
            }
        }
        catch (StripeException)
        {
            throw new InvalidOperationException("Stripe merchant credentials could not be verified.");
        }

        return client;
    }

    private static void ValidateReturnUrl(string? value, string origin)
    {
        if (value is null || value.Length > 2048 || value.Any(char.IsControl)
            || !Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps
            || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Fragment)
            || !string.Equals(uri.GetLeftPart(UriPartial.Authority), origin, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Stripe return URLs must use the configured storefront HTTPS origin.");
        }
    }

    private static async Task<PaymentProviderCheckoutSnapshot> ProjectAsync(
        StripeClient client, StripeConnectorBinding binding, Session session, string? knownPaymentIntentId,
        CancellationToken cancellationToken)
    {
        var tenantId = MetadataGuid(session.Metadata, "tenantId");
        var connectorId = MetadataGuid(session.Metadata, "connectorId");
        var paymentIntentId = MetadataGuid(session.Metadata, "paymentIntentId");
        var orderId = MetadataGuid(session.Metadata, "orderId");
        if (tenantId != binding.TenantId || connectorId != binding.ConnectorId || session.Livemode != binding.LiveMode
            || session.Mode != "payment" || session.UiMode != "hosted_page"
            || session.ClientReferenceId != paymentIntentId.ToString("N")
            || session.Currency != "gbp" || session.AmountTotal is null or <= 0
            || session.AfterExpiration?.Recovery?.Enabled == true || session.RecoveredFrom is not null
            || (knownPaymentIntentId is not null && session.PaymentIntentId != knownPaymentIntentId))
        {
            throw new InvalidOperationException("Stripe checkout evidence does not match its tenant and payment binding.");
        }

        var payment = session.PaymentIntent;
        if (payment is null && session.PaymentIntentId is not null)
        {
            payment = await client.V1.PaymentIntents.GetAsync(session.PaymentIntentId,
                new PaymentIntentGetOptions { Expand = ["latest_charge"] }, cancellationToken: cancellationToken);
        }

        if (payment is not null && (payment.Id != session.PaymentIntentId
            || payment.Livemode != binding.LiveMode || payment.Currency != session.Currency
            || payment.Amount != session.AmountTotal
            || MetadataGuid(payment.Metadata, "tenantId") != tenantId
            || MetadataGuid(payment.Metadata, "connectorId") != connectorId
            || MetadataGuid(payment.Metadata, "paymentIntentId") != paymentIntentId
            || MetadataGuid(payment.Metadata, "orderId") != orderId))
        {
            throw new InvalidOperationException("Stripe PaymentIntent evidence does not match its Checkout Session.");
        }

        var status = "Processing";
        var closed = false;
        if (session.Status == "complete" && session.PaymentStatus == "paid" && payment?.Status == "succeeded"
            && payment.AmountReceived == session.AmountTotal && payment.AmountCapturable == 0)
        {
            status = "Captured";
        }
        else if (session.Status == "expired" && session.PaymentStatus == "unpaid"
            && (payment is null || (payment.AmountReceived == 0 && payment.AmountCapturable == 0
                && payment.Status is "requires_payment_method" or "requires_confirmation" or "requires_action" or "canceled")))
        {
            // Stripe's expired hosted Session is terminal; its PI need not transition to canceled.
            status = "Cancelled";
            closed = true;
        }
        else if (session.Status == "open" && session.PaymentStatus == "unpaid"
            && (payment is null || (payment.AmountReceived == 0 && payment.AmountCapturable == 0
                && payment.Status is "requires_payment_method" or "requires_confirmation" or "requires_action")))
        {
            status = payment?.Status == "requires_action" ? "RequiresAction"
                : payment?.Status == "requires_payment_method" && payment.LastPaymentError is not null ? "Failed" : "Pending";
        }

        string? checkoutUrl = null;
        if (status is "Pending" or "RequiresAction" or "Failed")
        {
            if (!Uri.TryCreate(session.Url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps
                || uri.Host != "checkout.stripe.com" || !uri.IsDefaultPort || !string.IsNullOrEmpty(uri.UserInfo))
            {
                throw new InvalidOperationException("Stripe returned an invalid hosted checkout URL.");
            }

            checkoutUrl = session.Url;
        }

        return new PaymentProviderCheckoutSnapshot(tenantId, paymentIntentId, orderId, connectorId,
            binding.ProviderAccountId, binding.LiveMode, session.Id, session.PaymentIntentId,
            session.AmountTotal.Value / 100m, "GBP", status, closed, checkoutUrl,
            payment?.AmountReceived / 100m,
            status == "Captured" ? payment?.LatestCharge?.Created : null);
    }

    private static Guid MetadataGuid(IDictionary<string, string>? metadata, string key)
    {
        if (metadata is null || !metadata.TryGetValue(key, out var value) || !Guid.TryParseExact(value, "N", out var id) || id == Guid.Empty)
        {
            throw new InvalidOperationException("Stripe payment correlation is missing or invalid.");
        }

        return id;
    }
}
