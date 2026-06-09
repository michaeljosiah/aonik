using System.Net;
using System.Text;

using Aonik.Finance.Contracts.Services.Partners.Connectors;
using Aonik.Finance.Services.Partners.Connectors.Flutterwave;
using Aonik.Finance.Services.Partners.Connectors.Flutterwave.Bills;
using Aonik.SharedKernel.Primitives;
using FluentAssertions;

namespace Aonik.Application.Tests.Partners;

public class FlutterwaveBillPaymentConnectorTests
{
    // ── Test doubles ──────────────────────────────────────────────────────────
    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new();

        public List<Uri?> RequestedUris { get; } = new();
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
            RequestedUris.Add(request.RequestUri);
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            if (_responses.Count == 0)
            {
                throw new InvalidOperationException("No fake HTTP response was queued.");
            }

            return _responses.Dequeue()(request);
        }
    }

    private sealed class StaticBillsConfigProvider(FlutterwaveBillsOptions options) : IFlutterwaveBillsConfigProvider
    {
        public Task<FlutterwaveBillsOptions> GetAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(options);

        public Task<FlutterwaveBillsOptions> GetAsync(
            Aonik.Finance.Services.Partners.Connectors.ConnectorBinding binding,
            CancellationToken cancellationToken = default)
            => Task.FromResult(options);
    }

    private static FlutterwaveBillPaymentConnector CreateConnector(
        RecordingHandler handler, FlutterwaveBillsOptions? options = null)
    {
        var effective = options ?? ConfiguredOptions();
        var config = new StaticBillsConfigProvider(effective);
        var client = new FlutterwaveBillsClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://sandbox.flutterwave.test/v3/") });
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

    // ── Pay path (item-targeted) ───────────────────────────────────────────────
    [Fact]
    public async Task PayBillAsync_Should_PostToItemSpecificEndpoint_WithBillerAndItemCodes()
    {
        var handler = new RecordingHandler();
        handler.Enqueue(HttpStatusCode.OK,
            "{\"status\":\"success\",\"message\":\"Bill payment successful\",\"data\":{\"flw_ref\":\"FLW-REF-1\",\"tx_ref\":\"TX-1\"}}");
        var connector = CreateConnector(handler);
        // Note: data carries both flw_ref and tx_ref; the provider reference must be tx_ref.

        var instruction = new BillPaymentInstruction(
            ClientReference: "AONIK-BILL-1",
            BillerCode: "BIL112",
            ItemCode: "UB112",
            CustomerId: "1234567890",
            Amount: new Money(5000m, "NGN"),
            ValidationToken: null,
            ServiceCategory: PartnerServiceCategory.BillPayment,
            Inputs: null);

        var result = await connector.PayBillAsync(instruction);

        // The request must target the specific biller item, not a generic /bills + type call.
        handler.RequestedUris.Should().ContainSingle();
        handler.RequestedUris[0]!.AbsolutePath.Should().Contain("billers/BIL112/items/UB112/payment");
        // And it must send the documented customer_id field, not customer.
        handler.LastBody.Should().Contain("\"customer_id\":\"1234567890\"");
        handler.LastBody.Should().NotContain("\"customer\":");
        result.Status.Should().Be(PartnerTransactionStatus.Succeeded);
        // tx_ref is the value the status endpoint polls by, not flw_ref.
        result.Reference.ProviderReference.Should().Be("TX-1");
    }

    [Fact]
    public async Task PayBillAsync_Should_UseTxRef_WhenFlwRefAbsent()
    {
        var handler = new RecordingHandler();
        handler.Enqueue(HttpStatusCode.OK,
            "{\"status\":\"success\",\"data\":{\"tx_ref\":\"BPUSSD-123\",\"network\":\"MTN\"}}");
        var connector = CreateConnector(handler);

        var instruction = new BillPaymentInstruction(
            "AONIK-BILL-2", "BIL099", "AT099", "08038291822",
            new Money(500m, "NGN"), null, PartnerServiceCategory.AirtimeTopup, null);

        var result = await connector.PayBillAsync(instruction);

        // No flw_ref in the response — the provider reference must still be the tx_ref, never null.
        result.Reference.ProviderReference.Should().Be("BPUSSD-123");
    }

    [Fact]
    public async Task GetBillPaymentStatusAsync_Should_PollByProviderReference_TxRef()
    {
        var handler = new RecordingHandler();
        handler.Enqueue(HttpStatusCode.OK,
            "{\"status\":\"success\",\"data\":{\"tx_ref\":\"BPUSSD-123\",\"product_name\":\"MTN\"}}");
        var connector = CreateConnector(handler);

        // Provider reference = tx_ref captured at pay time.
        var reference = new PartnerReference("AONIK-BILL-2", "BPUSSD-123");

        var result = await connector.GetBillPaymentStatusAsync(reference);

        handler.RequestedUris.Should().ContainSingle();
        handler.RequestedUris[0]!.AbsolutePath.Should().Contain("bills/BPUSSD-123");
        result.Status.Should().Be(PartnerTransactionStatus.Succeeded);
    }

    [Fact]
    public async Task PayBillAsync_Should_FailClosed_WhenItemCodeMissing()
    {
        var handler = new RecordingHandler();
        var connector = CreateConnector(handler);

        var instruction = new BillPaymentInstruction(
            "ref", "BIL112", ItemCode: "", "customer",
            new Money(100m, "NGN"), null, PartnerServiceCategory.BillPayment, null);

        var act = () => connector.PayBillAsync(instruction);

        (await act.Should().ThrowAsync<FlutterwaveException>()).Which.ErrorType.Should().Be("NO_ITEM");
        handler.RequestedUris.Should().BeEmpty();
    }
}
