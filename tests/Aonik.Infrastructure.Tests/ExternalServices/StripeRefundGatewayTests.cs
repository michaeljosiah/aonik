using System.Net;
using System.Text.Json.Nodes;

using Aonik.Finance.Contracts.Services.Payments;

using FluentAssertions;
using OpenTelemetry;

namespace Aonik.Infrastructure.Tests.ExternalServices;

public partial class StripeCheckoutGatewayTests
{
    [Theory]
    [InlineData(0.01)]
    [InlineData(12.34)]
    public async Task RefundCreate_Should_SendExactCashAmountToOriginalPaymentWithStableKey(decimal amount)
    {
        using var test = new Harness();
        test.EnqueueRefundFunding();
        test.Responses.Enqueue(test.Refund(amount: amount));

        var result = await test.Gateway.CreateRefundAsync(test.RefundRequest with { Amount = amount });

        result.TenantId.Should().Be(test.TenantId);
        result.Amount.Should().Be(amount);
        result.ProviderPaymentIntentId.Should().Be("pi_one");
        result.ProviderChargeId.Should().Be("ch_original");
        result.Status.Should().Be("succeeded");
        var create = test.Requests.Last();
        create.Path.Should().Be("/v1/refunds");
        create.Body.Should().Contain("payment_intent=pi_one").And.Contain($"amount={amount * 100:0}")
            .And.Contain($"metadata[refundId]={test.RefundId:N}");
        create.Body.Should().NotContain("reason=").And.NotContain("instructions_email").And.NotContain("reverse_transfer")
            .And.NotContain("refund_application_fee");
        create.IdempotencyKey.Should().Be(test.RefundRequest.IdempotencyKey);
        create.StripeAccount.Should().BeNull();
        Sdk.SuppressInstrumentation.Should().BeFalse();
    }

    [Theory]
    [InlineData("pending", "pending")]
    [InlineData("requires_action", "requires_action")]
    [InlineData("failed", "failed")]
    [InlineData("canceled", "canceled")]
    [InlineData("future_status", "unknown")]
    public async Task RefundRead_Should_KeepProviderStateAndSafeEvidence(string providerStatus, string expected)
    {
        using var test = new Harness();
        test.EnqueueRefundFunding();
        var refund = test.Refund(status: providerStatus);
        refund["failure_reason"] = "lost_or_stolen_card";
        refund["failure_balance_transaction"] = "txn_failure";
        refund["description"] = "private@example.test secret_fixture";
        refund["destination_details"] = new JsonObject { ["private"] = "private@example.test" };
        test.Responses.Enqueue(refund);

        var result = await test.Gateway.GetRefundAsync(test.RefundRequest, "re_one");

        result.Status.Should().Be(expected);
        result.FailureCode.Should().Be("lost_or_stolen_card");
        result.FailureBalanceTransactionId.Should().Be("txn_failure");
        System.Text.Json.JsonSerializer.Serialize(result).Should().NotContain("private@example.test").And.NotContain("secret_fixture");
        test.Requests.Last().IdempotencyKey.Should().BeNull();
    }

    [Theory]
    [InlineData("pi")]
    [InlineData("tenant")]
    [InlineData("mode")]
    [InlineData("uncaptured")]
    [InlineData("charge")]
    public async Task RefundCreate_Should_RejectContradictoryOriginalFundingBeforePost(string fault)
    {
        using var test = new Harness();
        var payment = test.RefundPayment();
        if (fault == "pi") payment["id"] = "pi_other";
        if (fault == "tenant") payment["metadata"]!["tenantId"] = Guid.NewGuid().ToString("N");
        if (fault == "mode") payment["livemode"] = true;
        if (fault == "uncaptured") payment["latest_charge"]!["captured"] = false;
        if (fault == "charge") payment["latest_charge"]!["payment_intent"] = "pi_other";
        test.Responses.Enqueue(Account());
        test.Responses.Enqueue(payment);

        var act = () => test.Gateway.CreateRefundAsync(test.RefundRequest);

        await act.Should().ThrowAsync<InvalidOperationException>();
        test.Requests.Should().HaveCount(2);
        test.Requests.Should().NotContain(x => x.Path == "/v1/refunds");
    }

    [Theory]
    [InlineData("id")]
    [InlineData("amount")]
    [InlineData("currency")]
    [InlineData("pi")]
    [InlineData("charge")]
    [InlineData("metadata")]
    public async Task RefundRead_Should_RejectMismatchedRefundEvidence(string fault)
    {
        using var test = new Harness();
        var refund = test.Refund();
        if (fault == "id") refund["id"] = "re_other";
        if (fault == "amount") refund["amount"] = 500;
        if (fault == "currency") refund["currency"] = "usd";
        if (fault == "pi") refund["payment_intent"] = "pi_other";
        if (fault == "charge") refund["charge"] = "ch_other";
        if (fault == "metadata") refund["metadata"]!["refundId"] = Guid.NewGuid().ToString("N");
        test.EnqueueRefundFunding();
        test.Responses.Enqueue(refund);

        var act = () => test.Gateway.GetRefundAsync(test.RefundRequest, "re_one");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task RefundBudget_Should_PageOriginalPaymentAndExposeUncorrelatedExternalRefund()
    {
        using var test = new Harness();
        test.EnqueueRefundFunding();
        test.Responses.Enqueue(RefundPage(true, test.Refund()));
        var external = test.Refund("re_dashboard", amount: 2m);
        external["metadata"] = new JsonObject();
        test.Responses.Enqueue(RefundPage(false, external));

        var result = await test.Gateway.GetRefundBudgetAsync(test.RefundRequest);

        result.CapturedAmount.Should().Be(12.34m);
        result.Refunds.Should().HaveCount(2);
        result.Refunds[0].LocalRefundId.Should().Be(test.RefundId);
        result.Refunds[1].LocalRefundId.Should().BeNull();
        test.Requests[2].Query.Should().Contain("payment_intent=pi_one").And.Contain("limit=100");
        test.Requests[3].Query.Should().Contain("starting_after=re_one");
        test.Requests.Should().OnlyContain(x => x.IdempotencyKey == null);
    }

    [Fact]
    public async Task RefundFind_Should_ReturnOnlyOneFullyBoundMatchWithoutCreating()
    {
        using var test = new Harness();
        test.EnqueueRefundFunding();
        var external = test.Refund("re_external", amount: 2m);
        external["metadata"] = new JsonObject();
        test.Responses.Enqueue(RefundPage(true, external));
        test.Responses.Enqueue(RefundPage(false, test.Refund()));

        var result = await test.Gateway.FindRefundAsync(test.RefundRequest);

        result!.ProviderRefundId.Should().Be("re_one");
        test.Requests.Should().OnlyContain(x => x.IdempotencyKey == null && x.Body == "");
    }

    [Fact]
    public async Task RefundFind_Should_ReturnNullForCompleteAbsenceWithoutCreating()
    {
        using var test = new Harness();
        test.EnqueueRefundFunding();
        test.Responses.Enqueue(RefundPage(false));

        var result = await test.Gateway.FindRefundAsync(test.RefundRequest);

        result.Should().BeNull();
        test.Requests.Should().OnlyContain(x => x.Body == "" && x.IdempotencyKey == null);
    }

    [Theory]
    [InlineData("ambiguous")]
    [InlineData("duplicate")]
    [InlineData("empty-more")]
    [InlineData("bounded")]
    public async Task RefundFind_Should_RejectIncompleteOrAmbiguousHistory(string fault)
    {
        using var test = new Harness();
        test.EnqueueRefundFunding();
        if (fault == "ambiguous") test.Responses.Enqueue(RefundPage(false, test.Refund(), test.Refund("re_other")));
        if (fault == "duplicate") test.Responses.Enqueue(RefundPage(false, test.Refund(), test.Refund()));
        if (fault == "empty-more") test.Responses.Enqueue(RefundPage(true));
        if (fault == "bounded")
            for (var i = 0; i < 10; i++) test.Responses.Enqueue(RefundPage(true, test.Refund($"re_{i}")));

        var act = () => test.Gateway.FindRefundAsync(test.RefundRequest);

        await act.Should().ThrowAsync<InvalidOperationException>();
        test.Requests.Count.Should().BeLessThanOrEqualTo(12);
        test.Requests.Should().OnlyContain(x => x.IdempotencyKey == null);
    }

    [Fact]
    public async Task RefundCreate_Should_NotRetryOrExposeProviderFailure()
    {
        using var test = new Harness();
        test.EnqueueRefundFunding();
        test.Responses.Enqueue(new JsonObject { ["error"] = new JsonObject { ["type"] = "api_error", ["message"] = "private@example.test secret_fixture" } });
        test.Statuses.Enqueue(HttpStatusCode.OK);
        test.Statuses.Enqueue(HttpStatusCode.OK);
        test.Statuses.Enqueue(HttpStatusCode.InternalServerError);

        var act = () => test.Gateway.CreateRefundAsync(test.RefundRequest);

        var error = (await act.Should().ThrowAsync<InvalidOperationException>()).Which;
        error.ToString().Should().NotContain("private@example.test").And.NotContain("secret_fixture");
        test.Requests.Should().HaveCount(3);
        Sdk.SuppressInstrumentation.Should().BeFalse();
    }

    [Fact]
    public async Task RefundCreate_Should_PreserveAmbiguousTimeoutWithoutRetry()
    {
        using var test = new Harness { RefundFailure = new TaskCanceledException("Fixture timeout") };
        test.EnqueueRefundFunding();

        var act = () => test.Gateway.CreateRefundAsync(test.RefundRequest);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Stripe refund state could not be confirmed.");
        test.Requests.Should().HaveCount(3);
        Sdk.SuppressInstrumentation.Should().BeFalse();
    }

    [Fact]
    public async Task RefundCreate_Should_PreserveCallerCancellationBeforeAnyHttp()
    {
        using var test = new Harness();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var act = () => test.Gateway.CreateRefundAsync(test.RefundRequest, cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        test.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task RefundCreate_Should_RejectChangedMerchantBeforeOriginalPaymentRead()
    {
        using var test = new Harness();
        test.Responses.Enqueue(Account("acct_other"));

        var act = () => test.Gateway.CreateRefundAsync(test.RefundRequest);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*configured merchant*");
        test.Requests.Should().ContainSingle().Which.Path.Should().Be("/v1/account");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(0.001)]
    public async Task RefundCreate_Should_RejectInvalidMinorUnitAmountsBeforeHttp(decimal amount)
    {
        using var test = new Harness();

        var act = () => test.Gateway.CreateRefundAsync(test.RefundRequest with { Amount = amount });

        await act.Should().ThrowAsync<InvalidOperationException>();
        test.Requests.Should().BeEmpty();
    }

    private static JsonObject RefundPage(bool more, params JsonObject[] refunds) => new()
    {
        ["object"] = "list", ["url"] = "/v1/refunds", ["has_more"] = more,
        ["data"] = new JsonArray(refunds.Select(x => (JsonNode)x).ToArray()),
    };

    private sealed partial class Harness
    {
        public Guid RefundId { get; } = Guid.NewGuid();
        public Exception? RefundFailure { get; init; }
        public PaymentProviderRefundRequest RefundRequest => new(RefundId, IntentId, OrderId, ConnectorId,
            "acct_merchant", false, "pi_one", 1.23m, "GBP", $"refund:{RefundId:N}");

        public void EnqueueRefundFunding()
        {
            Responses.Enqueue(Account());
            Responses.Enqueue(RefundPayment());
        }

        public JsonObject RefundPayment() => new()
        {
            ["object"] = "payment_intent", ["id"] = "pi_one", ["status"] = "succeeded", ["currency"] = "gbp",
            ["amount"] = 1234, ["amount_received"] = 1234, ["amount_capturable"] = 0, ["livemode"] = false,
            ["metadata"] = Metadata(), ["client_secret"] = "secret_fixture",
            ["latest_charge"] = new JsonObject
            {
                ["object"] = "charge", ["id"] = "ch_original", ["payment_intent"] = "pi_one", ["amount"] = 1234,
                ["currency"] = "gbp", ["paid"] = true, ["captured"] = true, ["livemode"] = false,
            },
        };

        public JsonObject Refund(string id = "re_one", decimal amount = 1.23m, string status = "succeeded")
        {
            var metadata = Metadata();
            metadata["refundId"] = RefundId.ToString("N");
            return new JsonObject
            {
                ["object"] = "refund", ["id"] = id, ["payment_intent"] = "pi_one", ["charge"] = "ch_original",
                ["amount"] = (long)(amount * 100), ["currency"] = "gbp", ["status"] = status,
                ["metadata"] = metadata, ["created"] = 1791540000, ["balance_transaction"] = "txn_refund",
            };
        }
    }
}
