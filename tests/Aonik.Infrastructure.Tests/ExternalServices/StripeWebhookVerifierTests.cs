using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

using FluentAssertions;
using Stripe;

using Aonik.Infrastructure.ExternalServices.Stripe;

namespace Aonik.Infrastructure.Tests.ExternalServices;

public class StripeWebhookVerifierTests
{
    private static readonly DateTime Now = new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);
    private static readonly long Timestamp = new DateTimeOffset(Now).ToUnixTimeSeconds();
    private static readonly Guid IntentId = Guid.NewGuid();

    [Fact]
    public void Verify_Should_UseSdkSignatureAndRetainOnlyAllowlistedReferences()
    {
        var body = Payload().ToJsonString();
        var signature = EventUtility.GenerateSignatureHeader(body, "whsec_fixture", Timestamp);

        var result = new StripeWebhookVerifier().Verify(body, signature, ["whsec_fixture"], Now);

        result.Supported.Should().BeTrue();
        result.PaymentIntentId.Should().Be(IntentId);
        result.SessionId.Should().Be("cs_test_one");
        result.EventId.Should().Be("evt_fixture");
        result.PayloadHash.Should().Be(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body))));
        result.ToString().Should().NotContain("customer@example.test").And.NotContain("secret_do_not_store");
    }

    [Fact]
    public void Verify_Should_TryTheSuppliedUnexpiredPreviousSecret()
    {
        var body = Payload().ToJsonString();
        var signature = EventUtility.GenerateSignatureHeader(body, "whsec_previous", Timestamp);

        var result = new StripeWebhookVerifier().Verify(body, signature, ["whsec_current", "whsec_previous"], Now);

        result.Supported.Should().BeTrue();
    }

    [Theory]
    [InlineData("tampered")]
    [InlineData("old")]
    [InlineData("future")]
    [InlineData("api-version")]
    [InlineData("connect-account")]
    [InlineData("context")]
    [InlineData("malformed-signature")]
    public void Verify_Should_RejectInvalidSignatureVersionOrAccountContext(string fault)
    {
        var payload = Payload();
        if (fault == "api-version") payload["api_version"] = "2020-08-27";
        if (fault == "connect-account") payload["account"] = "acct_other";
        if (fault == "context") payload["context"] = "acct_other";
        var body = payload.ToJsonString();
        var timestamp = fault == "old" ? Timestamp - 301 : fault == "future" ? Timestamp + 301 : Timestamp;
        var signature = EventUtility.GenerateSignatureHeader(body, "whsec_fixture", timestamp);
        if (fault == "tampered") body = body.Replace("cs_test_one", "cs_test_other", StringComparison.Ordinal);
        if (fault == "malformed-signature") signature = "bad-signature";

        var act = () => new StripeWebhookVerifier().Verify(body, signature, ["whsec_fixture"], Now);

        act.Should().Throw<ArgumentException>().WithMessage("Invalid Stripe webhook.");
    }

    [Fact]
    public void Verify_Should_AcknowledgeUnsupportedSignedEventsWithoutTreatingThemAsPayments()
    {
        var payload = Payload();
        payload["type"] = "customer.created";
        payload["data"]!["object"] = new JsonObject { ["object"] = "customer", ["id"] = "cus_fixture" };
        var body = payload.ToJsonString();
        var signature = EventUtility.GenerateSignatureHeader(body, "whsec_fixture", Timestamp);

        var result = new StripeWebhookVerifier().Verify(body, signature, ["whsec_fixture"], Now);

        result.Supported.Should().BeFalse();
        result.PaymentIntentId.Should().BeNull();
        result.SessionId.Should().BeNull();
    }

    [Theory]
    [InlineData("refund.created")]
    [InlineData("refund.updated")]
    [InlineData("refund.failed")]
    public void Verify_Should_AllowlistRefundCorrelationWithoutRetainingSensitiveProviderFields(string eventType)
    {
        var id = Guid.NewGuid();
        var payload = Payload();
        var metadata = payload["data"]!["object"]!["metadata"]!.DeepClone().AsObject();
        metadata["refundId"] = id.ToString("N");
        payload["type"] = eventType;
        payload["data"]!["object"] = new JsonObject
        {
            ["object"] = "refund", ["id"] = "re_one", ["payment_intent"] = "pi_one", ["metadata"] = metadata,
            ["description"] = "secret_do_not_store", ["instructions_email"] = "customer@example.test",
            ["destination_details"] = new JsonObject { ["private"] = "customer@example.test" },
        };
        var body = payload.ToJsonString();
        var signature = EventUtility.GenerateSignatureHeader(body, "whsec_fixture", Timestamp);

        var result = new StripeWebhookVerifier().Verify(body, signature, ["whsec_fixture"], Now);

        result.Supported.Should().BeTrue();
        result.RefundId.Should().Be(id);
        result.ProviderRefundId.Should().Be("re_one");
        result.ProviderPaymentIntentId.Should().Be("pi_one");
        result.PaymentIntentId.Should().Be(IntentId);
        result.SessionId.Should().BeNull();
        result.ToString().Should().NotContain("customer@example.test").And.NotContain("secret_do_not_store");
    }

    [Fact]
    public void Verify_Should_PreserveExternalRefundProviderReferenceWithoutInventingLocalIds()
    {
        var payload = Payload();
        payload["type"] = "refund.created";
        payload["data"]!["object"] = new JsonObject
        {
            ["object"] = "refund", ["id"] = "re_dashboard", ["payment_intent"] = "pi_one", ["metadata"] = new JsonObject(),
        };
        var body = payload.ToJsonString();

        var result = new StripeWebhookVerifier().Verify(body,
            EventUtility.GenerateSignatureHeader(body, "whsec_fixture", Timestamp), ["whsec_fixture"], Now);

        result.Supported.Should().BeTrue();
        result.RefundId.Should().BeNull();
        result.PaymentIntentId.Should().BeNull();
        result.ProviderPaymentIntentId.Should().Be("pi_one");
        result.ProviderRefundId.Should().Be("re_dashboard");
    }

    private static JsonObject Payload() => new()
    {
        ["object"] = "event", ["id"] = "evt_fixture", ["type"] = "checkout.session.completed",
        ["api_version"] = StripeConfiguration.ApiVersion, ["livemode"] = false, ["created"] = Timestamp,
        ["data"] = new JsonObject
        {
            ["object"] = new JsonObject
            {
                ["object"] = "checkout.session", ["id"] = "cs_test_one", ["payment_intent"] = "pi_one",
                ["customer_email"] = "customer@example.test", ["client_secret"] = "secret_do_not_store",
                ["metadata"] = new JsonObject
                {
                    ["paymentIntentId"] = IntentId.ToString("N"), ["tenantId"] = Guid.NewGuid().ToString("N"),
                    ["connectorId"] = Guid.NewGuid().ToString("N"), ["orderId"] = Guid.NewGuid().ToString("N"),
                },
            },
        },
    };
}
