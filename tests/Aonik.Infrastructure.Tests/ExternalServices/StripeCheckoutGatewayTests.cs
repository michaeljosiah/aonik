using System.Net;
using System.Text;
using System.Text.Json.Nodes;

using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
using Moq;
using OpenTelemetry;

using Aonik.Finance.Contracts.Services.Payments;
using Aonik.Infrastructure.ExternalServices.Stripe;
using Aonik.SharedKernel.Abstractions.Multitenancy;

namespace Aonik.Infrastructure.Tests.ExternalServices;

public class StripeCheckoutGatewayTests
{
    [Fact]
    public async Task Create_Should_VerifyMerchantAndSendExactKeyedHostedCardPaymentWithoutSecretsInResult()
    {
        using var test = new Harness();
        test.Responses.Enqueue(Account());
        test.Responses.Enqueue(test.Session());

        var result = await test.Gateway.CreateIntentAsync(test.Request);

        Sdk.SuppressInstrumentation.Should().BeFalse();
        result.ProviderReference.Should().Be("cs_test_one");
        result.ClientSecret.Should().BeNull();
        result.Checkout!.Amount.Should().Be(12.34m);
        result.Checkout.CanNoLongerPay.Should().BeFalse();
        test.Requests.Should().HaveCount(2);
        test.Requests[0].Path.Should().Be("/v1/account");
        var create = test.Requests[1];
        create.Path.Should().Be("/v1/checkout/sessions");
        create.IdempotencyKey.Should().Be("attempt-key");
        create.Authorization.Should().Be("Bearer sk_test_fixture");
        create.StripeAccount.Should().BeNull();
        create.Body.Should().Contain("ui_mode=hosted_page").And.Contain("mode=payment")
            .And.Contain("[unit_amount]=1234").And.Contain("[currency]=gbp")
            .And.Contain("allowed_payment_method_types[0]=card")
            .And.Contain("after_expiration[recovery][enabled]=false")
            .And.Contain("adaptive_pricing[enabled]=false")
            .And.Contain($"payment_intent_data[metadata][paymentIntentId]={test.IntentId:N}");
        create.Body.Should().NotContain("customer_email").And.NotContain("secret_fixture");
    }

    [Fact]
    public async Task Create_Should_RefuseMisboundRotatedKeyBeforeCreatingAnything()
    {
        using var test = new Harness();
        test.Responses.Enqueue(Account("acct_wrong"));

        var act = () => test.Gateway.CreateIntentAsync(test.Request);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*configured merchant*");
        test.Requests.Should().ContainSingle().Which.Path.Should().Be("/v1/account");
    }

    [Theory]
    [InlineData("USD", "Card", 12.34, "https://shop.example/success")]
    [InlineData("GBP", "BankTransfer", 12.34, "https://shop.example/success")]
    [InlineData("GBP", "Card", 12.345, "https://shop.example/success")]
    [InlineData("GBP", "Card", 0, "https://shop.example/success")]
    [InlineData("GBP", "Card", 0.29, "https://shop.example/success")]
    [InlineData("GBP", "Card", 12.34, "https://evil.example/success")]
    [InlineData("GBP", "Card", 12.34, "https://shop.example@evil.example/success")]
    public async Task Create_Should_RejectInvalidInputsBeforeHttp(string currency, string method, decimal amount, string returnUrl)
    {
        using var test = new Harness();

        var act = () => test.Gateway.CreateIntentAsync(test.Request with
        {
            Currency = currency, PaymentMethodType = method, Amount = amount, ReturnUrl = returnUrl,
        });

        await act.Should().ThrowAsync<InvalidOperationException>();
        test.Requests.Should().BeEmpty();
    }

    [Theory]
    [InlineData("expired", "unpaid", null, 0, 0, "Cancelled", true)]
    [InlineData("expired", "unpaid", "canceled", 0, 0, "Cancelled", true)]
    [InlineData("expired", "unpaid", "requires_payment_method", 0, 0, "Cancelled", true)]
    [InlineData("expired", "unpaid", "requires_action", 0, 0, "Cancelled", true)]
    [InlineData("expired", "unpaid", "processing", 0, 0, "Processing", false)]
    [InlineData("expired", "unpaid", "requires_capture", 0, 1234, "Processing", false)]
    [InlineData("expired", "unpaid", "succeeded", 1234, 0, "Processing", false)]
    [InlineData("expired", "unpaid", "canceled", 1, 0, "Processing", false)]
    [InlineData("expired", "unpaid", "future_status", 0, 0, "Processing", false)]
    [InlineData("complete", "paid", "succeeded", 1234, 0, "Captured", false)]
    [InlineData("complete", "paid", "succeeded", 1200, 0, "Processing", false)]
    [InlineData("complete", "unpaid", "processing", 0, 0, "Processing", false)]
    [InlineData("open", "unpaid", "requires_action", 0, 0, "RequiresAction", false)]
    [InlineData("open", "unpaid", "requires_payment_method", 0, 0, "Pending", false)]
    public async Task Read_Should_ProjectOnlyVerifiedPaymentOrUnpaidClosure(
        string sessionStatus, string paymentStatus, string? intentStatus, int received, int capturable, string expected, bool closed)
    {
        using var test = new Harness();
        test.Responses.Enqueue(Account());
        test.Responses.Enqueue(test.Session(sessionStatus, paymentStatus, intentStatus, received, capturable));

        var result = await test.Gateway.GetCheckoutAsync(test.Reference);

        result.Status.Should().Be(expected);
        result.CanNoLongerPay.Should().Be(closed);
        System.Text.Json.JsonSerializer.Serialize(result).Should().NotContain("never_return_this");
        result.CheckoutUrl.Should().Be(expected is "Pending" or "RequiresAction" ? "https://checkout.stripe.com/c/pay/cs_test_one" : null);
    }

    [Fact]
    public async Task Read_Should_ReportDeclineWithoutClaimingPaymentIsClosed()
    {
        using var test = new Harness();
        var session = test.Session(intentStatus: "requires_payment_method");
        session["payment_intent"]!["last_payment_error"] = new JsonObject { ["code"] = "card_declined", ["type"] = "card_error" };
        test.Responses.Enqueue(Account());
        test.Responses.Enqueue(session);

        var result = await test.Gateway.GetCheckoutAsync(test.Reference);

        result.Status.Should().Be("Failed");
        result.CanNoLongerPay.Should().BeFalse();
        result.CheckoutUrl.Should().NotBeNull();
    }

    [Theory]
    [InlineData("missing-known-pi")]
    [InlineData("tenant")]
    [InlineData("mode")]
    [InlineData("recovery")]
    [InlineData("pi-metadata")]
    [InlineData("pi-amount")]
    public async Task Read_Should_RejectMissingOrContradictoryBindingEvidence(string fault)
    {
        using var test = new Harness();
        var session = test.Session("expired", "unpaid", fault == "missing-known-pi" ? null : "canceled");
        if (fault == "tenant") session["metadata"]!["tenantId"] = Guid.NewGuid().ToString("N");
        if (fault == "mode") session["livemode"] = true;
        if (fault == "recovery") session["after_expiration"] = new JsonObject { ["recovery"] = new JsonObject { ["enabled"] = true } };
        if (fault == "pi-metadata") session["payment_intent"]!["metadata"]!["orderId"] = Guid.NewGuid().ToString("N");
        if (fault == "pi-amount") session["payment_intent"]!["amount"] = 999;
        test.Responses.Enqueue(Account());
        test.Responses.Enqueue(session);

        var act = () => test.Gateway.GetCheckoutAsync(test.Reference with { ProviderPaymentIntentId = "pi_one" });

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Expire_Should_ReadCurrentSuccessWhenPaymentWinsRace()
    {
        using var test = new Harness();
        test.Responses.Enqueue(Account());
        test.Responses.Enqueue(new JsonObject { ["error"] = new JsonObject { ["type"] = "invalid_request_error", ["message"] = "Not open" } });
        test.Responses.Enqueue(test.Session("complete", "paid", "succeeded", 1234));
        test.Statuses.Enqueue(HttpStatusCode.OK);
        test.Statuses.Enqueue(HttpStatusCode.BadRequest);
        test.Statuses.Enqueue(HttpStatusCode.OK);

        var result = await test.Gateway.ExpireCheckoutAsync(test.Reference);

        result.Status.Should().Be("Captured");
        result.CanNoLongerPay.Should().BeFalse();
        test.Requests.Select(r => r.Path).Should().Equal("/v1/account", "/v1/checkout/sessions/cs_test_one/expire", "/v1/checkout/sessions/cs_test_one");
    }

    [Fact]
    public async Task Create_Should_NotRetryProviderFailureAndShouldSanitizeError()
    {
        using var test = new Harness();
        test.Responses.Enqueue(Account());
        test.Responses.Enqueue(new JsonObject { ["error"] = new JsonObject { ["type"] = "api_error", ["message"] = "secret_fixture customer@example.test" } });
        test.Statuses.Enqueue(HttpStatusCode.OK);
        test.Statuses.Enqueue(HttpStatusCode.InternalServerError);

        var act = () => test.Gateway.CreateIntentAsync(test.Request);

        var error = (await act.Should().ThrowAsync<InvalidOperationException>()).Which;
        Sdk.SuppressInstrumentation.Should().BeFalse();
        error.ToString().Should().NotContain("secret_fixture").And.NotContain("customer@example.test");
        test.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task Create_Should_PreserveCallerCancellation()
    {
        using var test = new Harness();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var act = () => test.Gateway.CreateIntentAsync(test.Request, cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        Sdk.SuppressInstrumentation.Should().BeFalse();
        test.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Registration_Should_RemoveGlobalRetriesAndConfigureBounds()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.ConfigureHttpClientDefaults(client => client.AddStandardResilienceHandler());
        services.AddInfrastructure(new ConfigurationBuilder().Build(), Mock.Of<IHostEnvironment>(e => e.EnvironmentName == "Testing"));
        await using var provider = services.BuildServiceProvider();
        using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(IPaymentProviderGateway));
        var handler = provider.GetRequiredService<IHttpMessageHandlerFactory>().CreateHandler(nameof(IPaymentProviderGateway));

        client.Timeout.Should().Be(StripeCheckoutGateway.RequestTimeout);
        client.MaxResponseContentBufferSize.Should().Be(StripeCheckoutGateway.MaxResponseBytes);
        while (handler is DelegatingHandler next)
        {
            handler.Should().NotBeOfType<ResilienceHandler>();
            handler = next.InnerHandler!;
        }
        handler.Should().BeOfType<HttpClientHandler>().Which.AllowAutoRedirect.Should().BeFalse();
    }

    private static JsonObject Account(string id = "acct_merchant") => new() { ["object"] = "account", ["id"] = id };

    private sealed record ObservedRequest(string Path, string Body, string? Authorization, string? IdempotencyKey, string? StripeAccount);

    private sealed class Harness : IDisposable
    {
        public Guid TenantId { get; } = Guid.NewGuid();
        public Guid ConnectorId { get; } = Guid.NewGuid();
        public Guid IntentId { get; } = Guid.NewGuid();
        public Guid OrderId { get; } = Guid.NewGuid();
        public Queue<JsonObject> Responses { get; } = new();
        public Queue<HttpStatusCode> Statuses { get; } = new();
        public List<ObservedRequest> Requests { get; } = [];
        private readonly HttpClient _client;
        public StripeCheckoutGateway Gateway { get; }
        public PaymentProviderIntentRequest Request => new(OrderId, 12.34m, "GBP", "Card", "https://shop.example/success",
            "https://shop.example/cancel", "order", IntentId, ConnectorId, "acct_merchant", false, "attempt-key");
        public PaymentProviderCheckoutReference Reference => new(ConnectorId, "acct_merchant", false, "cs_test_one");

        public Harness()
        {
            var binding = new StripeConnectorBinding
            {
                TenantId = TenantId, ConnectorId = ConnectorId, ProviderAccountId = "acct_merchant", LiveMode = false,
                ReturnOrigin = "https://shop.example", SecretKey = "sk_test_fixture", SigningSecrets = ["whsec_fixture"],
            };
            var resolver = new Mock<IStripeConnectorResolver>();
            resolver.Setup(r => r.ResolveBoundAsync(ConnectorId, It.IsAny<CancellationToken>())).ReturnsAsync(binding);
            _client = new HttpClient(new Handler(async (request, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                Sdk.SuppressInstrumentation.Should().BeTrue();
                request.RequestUri!.Host.Should().Be("api.stripe.com");
                var body = request.Content is null ? "" : Uri.UnescapeDataString(await request.Content.ReadAsStringAsync(ct));
                Requests.Add(new ObservedRequest(request.RequestUri.AbsolutePath, body, request.Headers.Authorization?.ToString(),
                    request.Headers.TryGetValues("Idempotency-Key", out var keys) ? keys.Single() : null,
                    request.Headers.TryGetValues("Stripe-Account", out var accounts) ? accounts.Single() : null));
                return new HttpResponseMessage(Statuses.Count > 0 ? Statuses.Dequeue() : HttpStatusCode.OK)
                {
                    Content = new StringContent(Responses.Dequeue().ToJsonString(), Encoding.UTF8, "application/json"),
                };
            }));
            Gateway = new StripeCheckoutGateway(_client, resolver.Object,
                Mock.Of<ITenantProvider>(p => p.GetCurrentTenantId() == TenantId));
        }

        private JsonObject Metadata() => new()
        {
            ["tenantId"] = TenantId.ToString("N"), ["connectorId"] = ConnectorId.ToString("N"),
            ["paymentIntentId"] = IntentId.ToString("N"), ["orderId"] = OrderId.ToString("N"),
        };

        public JsonObject Session(string status = "open", string paymentStatus = "unpaid", string? intentStatus = null, int received = 0, int capturable = 0)
            => new()
            {
                ["object"] = "checkout.session", ["id"] = "cs_test_one", ["mode"] = "payment", ["ui_mode"] = "hosted_page",
                ["status"] = status, ["payment_status"] = paymentStatus, ["currency"] = "gbp", ["amount_total"] = 1234,
                ["client_reference_id"] = IntentId.ToString("N"), ["metadata"] = Metadata(), ["livemode"] = false,
                ["url"] = "https://checkout.stripe.com/c/pay/cs_test_one",
                ["payment_intent"] = intentStatus is null ? null : new JsonObject
                {
                    ["object"] = "payment_intent", ["id"] = "pi_one", ["amount"] = 1234, ["currency"] = "gbp", ["livemode"] = false,
                    ["amount_received"] = received, ["amount_capturable"] = capturable, ["status"] = intentStatus, ["metadata"] = Metadata(),
                    ["client_secret"] = "never_return_this", ["latest_charge"] = null,
                },
            };

        public void Dispose() => _client.Dispose();
    }

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => send(request, cancellationToken);
    }
}
