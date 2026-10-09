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
