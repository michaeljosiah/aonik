using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Aonik.Finance.Contracts.Services.Partners.Connectors;
using Aonik.Finance.Services.Partners.Connectors.Flutterwave;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Aonik.Application.Tests.Partners;

public class FlutterwaveConnectorTests
{
    // ── Test doubles ──────────────────────────────────────────────────────────
    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new();

        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastBody { get; private set; }

        public void Enqueue(HttpStatusCode status, string json)
            => _responses.Enqueue(_ => new HttpResponseMessage(status)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });

        public void EnqueueThrow(Exception ex) => _responses.Enqueue(_ => throw ex);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            if (_responses.Count == 0)
            {
                throw new InvalidOperationException("No fake HTTP response was queued.");
            }

            return _responses.Dequeue()(request);
        }
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;
        private readonly Uri _baseAddress;
        public StubHttpClientFactory(HttpMessageHandler handler, Uri baseAddress)
        {
            _handler = handler;
            _baseAddress = baseAddress;
        }

        public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false) { BaseAddress = _baseAddress };
    }

    private sealed class TestTimeProvider : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UnixEpoch;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static FlutterwavePayoutConnector CreateConnector(RecordingHandler handler, FlutterwaveOptions? options = null)
    {
        var client = new FlutterwaveClient(new HttpClient(handler) { BaseAddress = new Uri("https://sandbox.flutterwave.test/") });
        return new FlutterwavePayoutConnector(client, Microsoft.Extensions.Options.Options.Create(options ?? new FlutterwaveOptions()));
    }

    private static string Envelope(string dataJson) => $"{{\"status\":\"success\",\"data\":{dataJson}}}";

    // ── Initiate payout ───────────────────────────────────────────────────────
    [Fact]
    public async Task InitiatePayoutAsync_Should_PostTransfer_ViaRecipientId_AndMapResult()
    {
        var handler = new RecordingHandler();
        handler.Enqueue(HttpStatusCode.Created, Envelope(
            "{\"id\":\"trf_999\",\"reference\":\"AONIKREM0001\",\"status\":\"NEW\",\"fee\":{\"currency\":\"GBP\",\"value\":12.50}}"));
        var connector = CreateConnector(handler, new FlutterwaveOptions { DefaultTransferPurpose = "family_maintenance" });

        var instruction = new PayoutInstruction(
            ClientReference: "REM-0a1b2c3d",
            Amount: new SharedKernel.Primitives.Money(50000m, "NGN"),
            DebitCurrency: "GBP",
            Destination: new BankAccountDestination("044", "rcp_abc123", null, "John Doe"),
            Narration: "Remittance",
            CallbackUrl: null,
            Metadata: new Dictionary<string, string> { ["transfer_purpose"] = "education" });

        var result = await connector.InitiatePayoutAsync(instruction);

        result.Status.Should().Be(PartnerTransactionStatus.Pending); // NEW → Pending
        result.Reference.ClientReference.Should().Be("REM-0a1b2c3d");
        result.Reference.ProviderReference.Should().Be("trf_999");
        result.Fee.Should().NotBeNull();
        result.Fee!.Amount.Should().Be(12.50m);

        handler.LastRequest!.RequestUri!.AbsolutePath.Should().Be("/transfers");
        handler.LastRequest.Headers.GetValues("X-Idempotency-Key").Single()
            .Should().Be(FlutterwaveReferences.IdempotencyKeyFrom("REM-0a1b2c3d"));

        using var body = JsonDocument.Parse(handler.LastBody!);
        var root = body.RootElement;
        root.GetProperty("reference").GetString().Should().Be("REM0a1b2c3d"); // hyphen sanitized
        root.GetProperty("transfer_purpose").GetString().Should().Be("education");
        root.GetProperty("payment_instruction").GetProperty("recipient_id").GetString().Should().Be("rcp_abc123");
        root.GetProperty("meta").GetProperty("aonik_client_reference").GetString().Should().Be("REM-0a1b2c3d");
    }

    [Fact]
    public async Task InitiatePayoutAsync_Should_FailClosed_When_DestinationHasNoRecipientId()
    {
        var handler = new RecordingHandler();
        var connector = CreateConnector(handler);
        var instruction = new PayoutInstruction(
            "REM-1", new SharedKernel.Primitives.Money(100m, "NGN"), "GBP",
            new BankAccountDestination("044", "****1234", null, "John Doe"), "n", null, null);

        var act = () => connector.InitiatePayoutAsync(instruction);

        (await act.Should().ThrowAsync<FlutterwaveException>()).Which.Retryable.Should().BeFalse();
    }

    [Fact]
    public async Task InitiatePayoutAsync_Should_ThrowRetryable_OnPartner5xx()
    {
        var handler = new RecordingHandler();
        handler.Enqueue(HttpStatusCode.InternalServerError, "{}");
        var connector = CreateConnector(handler);
        var instruction = Bank("REM-1");

        var act = () => connector.InitiatePayoutAsync(instruction);

        (await act.Should().ThrowAsync<FlutterwaveException>()).Which.Retryable.Should().BeTrue();
    }

    [Fact]
    public async Task InitiatePayoutAsync_Should_ThrowTimeout_OnTransportCancellation()
    {
        var handler = new RecordingHandler();
        handler.EnqueueThrow(new TaskCanceledException("timeout", new TimeoutException()));
        var connector = CreateConnector(handler);

        var act = () => connector.InitiatePayoutAsync(Bank("REM-1"));

        await act.Should().ThrowAsync<TimeoutException>();
    }

    [Fact]
    public async Task InitiatePayoutAsync_Should_ThrowNonRetryable_On422_WithParsedError()
    {
        var handler = new RecordingHandler();
        handler.Enqueue((HttpStatusCode)422,
            "{\"status\":\"failed\",\"error\":{\"type\":\"REQUEST_NOT_VALID\",\"code\":\"10400\",\"message\":\"Request is not valid\"}}");
        var connector = CreateConnector(handler);

        var ex = (await ((Func<Task>)(() => connector.InitiatePayoutAsync(Bank("REM-1"))))
            .Should().ThrowAsync<FlutterwaveException>()).Which;

        ex.Retryable.Should().BeFalse();
        ex.ErrorCode.Should().Be("10400");
        ex.ErrorType.Should().Be("REQUEST_NOT_VALID");
    }

    // ── Status polling ────────────────────────────────────────────────────────
    [Theory]
    [InlineData("NEW", PartnerTransactionStatus.Pending)]
    [InlineData("PENDING", PartnerTransactionStatus.Processing)]
    [InlineData("SUCCESSFUL", PartnerTransactionStatus.Succeeded)]
    [InlineData("FAILED", PartnerTransactionStatus.Failed)]
    [InlineData("CANCELLED", PartnerTransactionStatus.Failed)]
    [InlineData("WAT", PartnerTransactionStatus.Unknown)]
    public async Task GetPayoutStatusAsync_Should_MapTransferStatus(string raw, PartnerTransactionStatus expected)
    {
        var handler = new RecordingHandler();
        handler.Enqueue(HttpStatusCode.OK, Envelope($"{{\"id\":\"trf_1\",\"status\":\"{raw}\"}}"));
        var connector = CreateConnector(handler);

        var result = await connector.GetPayoutStatusAsync(new PartnerReference("REM-1", "trf_1"));

        result.Status.Should().Be(expected);
        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LastRequest.RequestUri!.AbsolutePath.Should().Be("/transfers/trf_1");
    }

    // ── Quote ─────────────────────────────────────────────────────────────────
    [Fact]
    public async Task QuotePayoutAsync_Should_ReturnRateAndConverted_WithNullFee()
    {
        var handler = new RecordingHandler();
        handler.Enqueue(HttpStatusCode.OK, Envelope(
            "{\"id\":\"rte_1\",\"rate\":\"2000.5\",\"source\":{\"amount\":\"25.00\",\"currency\":\"GBP\"},\"destination\":{\"amount\":\"50000\",\"currency\":\"NGN\"}}"));
        var connector = CreateConnector(handler);

        var result = await connector.QuotePayoutAsync(
            new PayoutQuoteRequest(new SharedKernel.Primitives.Money(50000m, "GBP"), "NGN", null));

        result.Fee.Should().BeNull(); // G8 — fee not quotable pre-send
        result.FxRate.Should().Be(2000.5m);
        result.ConvertedAmount!.Amount.Should().Be(25.00m);
        result.ConvertedAmount.Currency.Should().Be("GBP");
    }

    [Fact]
    public async Task QuotePayoutAsync_Should_ShortCircuit_SameCurrency()
    {
        var handler = new RecordingHandler(); // nothing enqueued — must not call the API
        var connector = CreateConnector(handler);

        var result = await connector.QuotePayoutAsync(
            new PayoutQuoteRequest(new SharedKernel.Primitives.Money(100m, "NGN"), "NGN", null));

        result.Fee.Should().BeNull();
        result.FxRate.Should().BeNull();
        handler.LastRequest.Should().BeNull();
    }

    // ── Account resolution + recipient registration ───────────────────────────
    [Fact]
    public async Task ResolveAccountAsync_Should_ReturnName()
    {
        var handler = new RecordingHandler();
        handler.Enqueue(HttpStatusCode.OK, Envelope(
            "{\"bank_code\":\"044\",\"account_number\":\"0690000040\",\"account_name\":\"Alex James\"}"));
        var connector = CreateConnector(handler);

        var result = await connector.ResolveAccountAsync(new AccountResolutionRequest("044", "0690000040", "NGN"));

        result.Resolved.Should().BeTrue();
        result.AccountName.Should().Be("Alex James");
        handler.LastRequest!.RequestUri!.AbsolutePath.Should().Be("/banks/account-resolve");
    }

    [Fact]
    public async Task ResolveAccountAsync_Should_Throw_When_CurrencyMissing()
    {
        var connector = CreateConnector(new RecordingHandler());
        var act = () => connector.ResolveAccountAsync(new AccountResolutionRequest("044", "0690000040", null));
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task RegisterRecipientAsync_Should_PostBankRecipient_AndReturnBeneficiaryId()
    {
        var handler = new RecordingHandler();
        handler.Enqueue(HttpStatusCode.Created, Envelope("{\"id\":\"rcp_B9aAgsdzzl\",\"type\":\"bank_ngn\"}"));
        var connector = CreateConnector(handler);

        var result = await connector.RegisterRecipientAsync(new RecipientRegistrationRequest(
            new BankAccountDestination("044", "0690000040", null, "Alex James"), "NGN", "Alex James", "NG", null));

        result.Registered.Should().BeTrue();
        result.ProviderBeneficiaryId.Should().Be("rcp_B9aAgsdzzl");
        handler.LastRequest!.RequestUri!.AbsolutePath.Should().Be("/transfers/recipients");

        using var body = JsonDocument.Parse(handler.LastBody!);
        body.RootElement.GetProperty("type").GetString().Should().Be("bank_ngn");
        body.RootElement.GetProperty("bank").GetProperty("account_number").GetString().Should().Be("0690000040");
        body.RootElement.GetProperty("name").GetProperty("first").GetString().Should().Be("Alex");
        body.RootElement.GetProperty("name").GetProperty("last").GetString().Should().Be("James");
    }

    // ── Webhook translator ────────────────────────────────────────────────────
    [Fact]
    public void Webhook_VerifySignature_Should_Pass_ForCorrectHmac_AndFail_ForTampered()
    {
        var translator = new FlutterwaveWebhookTranslator();
        const string secret = "whsec_test";
        const string bodyText = "{\"type\":\"transfer.disburse\",\"data\":{\"id\":\"trf_1\",\"status\":\"SUCCESSFUL\"}}";
        var signature = Convert.ToBase64String(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(bodyText)));

        // Header casing intentionally differs (case-insensitive lookup, G15).
        var envelope = new PartnerWebhookEnvelope(
            "Flutterwave",
            new Dictionary<string, string> { ["Flutterwave-Signature"] = signature },
            bodyText);

        translator.VerifySignature(envelope, secret).Should().BeTrue();
        translator.VerifySignature(envelope, "wrong-secret").Should().BeFalse();
        translator.VerifySignature(envelope, "").Should().BeFalse();
        translator.VerifySignature(
            new PartnerWebhookEnvelope("Flutterwave", new Dictionary<string, string>(), bodyText), secret)
            .Should().BeFalse();
    }

    [Fact]
    public void Webhook_Translate_Should_ReconstructClientReference_FromMeta()
    {
        var translator = new FlutterwaveWebhookTranslator();
        const string bodyText =
            "{\"type\":\"transfer.disburse\",\"data\":{\"id\":\"trf_1\",\"reference\":\"REM0a1b\",\"status\":\"SUCCESSFUL\","
            + "\"meta\":{\"aonik_client_reference\":\"REM-0a1b2c3d\"}}}";

        var evt = translator.Translate(new PartnerWebhookEnvelope(
            "Flutterwave", new Dictionary<string, string>(), bodyText));

        evt.Category.Should().Be(PartnerServiceCategory.Payout);
        evt.Status.Should().Be(PartnerTransactionStatus.Succeeded);
        evt.Reference.ClientReference.Should().Be("REM-0a1b2c3d"); // exact stored value (G17)
        evt.Reference.ProviderReference.Should().Be("trf_1");
    }

    [Fact]
    public void Webhook_Translate_Should_NotThrow_OnMalformedBody()
    {
        var translator = new FlutterwaveWebhookTranslator();
        var evt = translator.Translate(new PartnerWebhookEnvelope(
            "Flutterwave", new Dictionary<string, string>(), "}{ not json"));

        evt.Status.Should().Be(PartnerTransactionStatus.Unknown);
    }

    // ── Token provider ────────────────────────────────────────────────────────
    [Fact]
    public async Task TokenProvider_Should_CacheToken_AndRefreshAfterExpiry()
    {
        var handler = new RecordingHandler();
        handler.Enqueue(HttpStatusCode.OK, "{\"access_token\":\"tok1\",\"expires_in\":600,\"token_type\":\"Bearer\"}");
        handler.Enqueue(HttpStatusCode.OK, "{\"access_token\":\"tok2\",\"expires_in\":600,\"token_type\":\"Bearer\"}");

        var clock = new TestTimeProvider();
        var factory = new StubHttpClientFactory(handler, new Uri("https://idp.flutterwave.test/token"));
        var provider = new FlutterwaveTokenProvider(
            factory, Microsoft.Extensions.Options.Options.Create(new FlutterwaveOptions { ClientId = "id", ClientSecret = "secret" }))
        {
            Clock = clock
        };

        (await provider.GetAccessTokenAsync(CancellationToken.None)).Should().Be("tok1");
        (await provider.GetAccessTokenAsync(CancellationToken.None)).Should().Be("tok1"); // cached, no 2nd call

        clock.Now = clock.Now.AddSeconds(601); // past (600 - 60) refresh window
        (await provider.GetAccessTokenAsync(CancellationToken.None)).Should().Be("tok2");
    }

    private static PayoutInstruction Bank(string clientReference) => new(
        clientReference,
        new SharedKernel.Primitives.Money(50000m, "NGN"),
        "GBP",
        new BankAccountDestination("044", "rcp_abc123", null, "John Doe"),
        "Remittance",
        null,
        null);
}
