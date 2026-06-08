using System.Net;
using System.Text;

using Aonik.Finance.Contracts.Services.Partners.Connectors;
using Aonik.Finance.Services.Partners.Connectors.Flutterwave;
using Aonik.Finance.Services.Partners.Connectors.Flutterwave.Bills;
using FluentAssertions;

namespace Aonik.Application.Tests.Partners;

public class FlutterwaveBillPaymentConnectorTests
{
    // ── Test doubles ──────────────────────────────────────────────────────────
    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new();

        public List<Uri?> RequestedUris { get; } = new();

        public void Enqueue(HttpStatusCode status, string json)
            => _responses.Enqueue(_ => new HttpResponseMessage(status)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });

        public void EnqueueThrow(Exception ex) => _responses.Enqueue(_ => throw ex);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestedUris.Add(request.RequestUri);
            if (_responses.Count == 0)
            {
                throw new InvalidOperationException("No fake HTTP response was queued.");
            }

            return Task.FromResult(_responses.Dequeue()(request));
        }
    }

    private sealed class StaticBillsConfigProvider(FlutterwaveBillsOptions options) : IFlutterwaveBillsConfigProvider
    {
        public Task<FlutterwaveBillsOptions> GetAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(options);
    }

    private static FlutterwaveBillPaymentConnector CreateConnector(
        RecordingHandler handler, FlutterwaveBillsOptions? options = null)
    {
        var effective = options ?? ConfiguredOptions();
        var config = new StaticBillsConfigProvider(effective);
        var client = new FlutterwaveBillsClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://sandbox.flutterwave.test/v3/") },
            config);
        return new FlutterwaveBillPaymentConnector(client, config);
    }

    private static FlutterwaveBillsOptions ConfiguredOptions() => new()
    {
        Enabled = true,
        BaseUrl = "https://sandbox.flutterwave.test/v3",
        SecretKey = "FLWSECK-test",
        Country = "NG"
    };

    private static string Envelope(string dataJson) => $"{{\"status\":\"success\",\"data\":{dataJson}}}";

    // ── Catalogue mapping ─────────────────────────────────────────────────────
    [Fact]
    public async Task GetBillerCatalogAsync_Should_MapCategoryRows_AndClassifyServiceCategory()
    {
        var handler = new RecordingHandler();
        handler.Enqueue(HttpStatusCode.OK, Envelope(
            """
            [
              { "biller_code": "BIL099", "item_code": "AT099", "name": "MTN Nigeria", "short_name": "MTN",
                "biller_name": "AIRTIME", "is_airtime": true, "country": "NG", "label_name": "Mobile Number" },
              { "biller_code": "BIL112", "item_code": "UB112", "name": "Ikeja Electric", "short_name": "IKEDC",
                "biller_name": "UTILITY", "is_airtime": false, "country": "NG", "label_name": "Meter Number" }
            ]
            """));
        var connector = CreateConnector(handler);

        var result = await connector.GetBillerCatalogAsync(new BillerCatalogQuery(null, "NG", null));

        result.Should().HaveCount(2);

        var mtn = result.Single(e => e.BillerCode == "BIL099");
        mtn.ServiceCategory.Should().Be(PartnerServiceCategory.AirtimeTopup);
        mtn.CategoryCode.Should().Be("AIRTIME");
        mtn.CustomerFields.Should().ContainSingle().Which.Label.Should().Be("Mobile Number");
        mtn.Items.Should().ContainSingle();
        mtn.Items[0].ItemCode.Should().Be("AT099");
        mtn.Items[0].AmountType.Should().Be(BillAmountType.Variable);

        var ikedc = result.Single(e => e.BillerCode == "BIL112");
        ikedc.ServiceCategory.Should().Be(PartnerServiceCategory.BillPayment);
    }

    [Fact]
    public async Task GetBillerCatalogAsync_Should_ExpandProducts_ForSelectedBillers_WhenRequested()
    {
        var handler = new RecordingHandler();
        handler.Enqueue(HttpStatusCode.OK, Envelope(
            """
            [ { "biller_code": "BIL112", "item_code": "UB112", "name": "Ikeja Electric", "short_name": "IKEDC",
                "biller_name": "UTILITY", "is_airtime": false, "country": "NG", "label_name": "Meter Number" } ]
            """));
        handler.Enqueue(HttpStatusCode.OK, Envelope(
            """
            { "biller_code": "BIL112", "products": [
              { "code": "PREPAID", "name": "Prepaid", "amount": "0.0", "fee": "0.0" },
              { "code": "BUNDLE5K", "name": "5000 Bundle", "amount": "5000.0", "fee": "0.0" } ] }
            """));
        var connector = CreateConnector(handler);

        var result = await connector.GetBillerCatalogAsync(new BillerCatalogQuery(
            CategoryCode: null, Country: "NG", Currency: null,
            BillerCodes: new[] { "BIL112" }, ExpandItems: true));

        var entry = result.Should().ContainSingle().Subject;
        entry.Items.Should().HaveCount(2);

        var prepaid = entry.Items.Single(i => i.ItemCode == "PREPAID");
        prepaid.AmountType.Should().Be(BillAmountType.Variable);
        prepaid.FixedAmount.Should().BeNull();

        var bundle = entry.Items.Single(i => i.ItemCode == "BUNDLE5K");
        bundle.AmountType.Should().Be(BillAmountType.Fixed);
        bundle.FixedAmount!.Amount.Should().Be(5000m);
        bundle.FixedAmount.Currency.Should().Be("NGN");
    }

    [Fact]
    public async Task GetBillerCatalogAsync_Should_ReturnEmpty_AndNotCallApi_ForNonNgCountry()
    {
        var handler = new RecordingHandler(); // no response queued — a call would throw
        var connector = CreateConnector(handler);

        var result = await connector.GetBillerCatalogAsync(new BillerCatalogQuery(null, "GB", null));

        result.Should().BeEmpty();
        handler.RequestedUris.Should().BeEmpty();
    }

    // ── Failure modes ─────────────────────────────────────────────────────────
    [Fact]
    public async Task GetBillerCatalogAsync_Should_ThrowRetryable_OnPartner5xx()
    {
        var handler = new RecordingHandler();
        handler.Enqueue(HttpStatusCode.InternalServerError, "{}");
        var connector = CreateConnector(handler);

        var act = () => connector.GetBillerCatalogAsync(new BillerCatalogQuery(null, "NG", null));

        (await act.Should().ThrowAsync<FlutterwaveException>()).Which.Retryable.Should().BeTrue();
    }

    [Fact]
    public async Task GetBillerCatalogAsync_Should_ThrowTimeout_OnTransportCancellation()
    {
        var handler = new RecordingHandler();
        handler.EnqueueThrow(new TaskCanceledException("timeout", new TimeoutException()));
        var connector = CreateConnector(handler);

        var act = () => connector.GetBillerCatalogAsync(new BillerCatalogQuery(null, "NG", null));

        await act.Should().ThrowAsync<TimeoutException>();
    }

    [Fact]
    public async Task GetBillerCatalogAsync_Should_FailClosed_WhenNotConfigured()
    {
        var handler = new RecordingHandler();
        var connector = CreateConnector(handler, new FlutterwaveBillsOptions { Enabled = false });

        var act = () => connector.GetBillerCatalogAsync(new BillerCatalogQuery(null, "NG", null));

        (await act.Should().ThrowAsync<FlutterwaveException>()).Which.ErrorType.Should().Be("CONFIGURATION");
    }
}
