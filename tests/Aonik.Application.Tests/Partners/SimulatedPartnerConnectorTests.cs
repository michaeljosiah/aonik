using FluentAssertions;

using Aonik.Finance.Contracts.Services.Partners.Connectors;
using Aonik.Finance.Services.Partners.Connectors;
using Aonik.SharedKernel.Primitives;

namespace Aonik.Application.Tests.Partners;

public class SimulatedPartnerConnectorTests
{
    private static SimulatedPartnerConnector CreateConnector() => new();

    private static PartnerConnectorResolver CreateResolver(SimulatedPartnerConnector connector)
        => new(
            new IPartnerPayoutConnector[] { connector },
            new IPartnerCollectionConnector[] { connector },
            new IPartnerBillPaymentConnector[] { connector },
            new IPartnerWebhookTranslator[] { new SimulatedPartnerWebhookTranslator() });

    [Fact]
    public void ProviderCode_Should_BeSimulated_And_AdvertiseLanePerCategory()
    {
        var connector = CreateConnector();

        connector.ProviderCode.Should().Be("Simulated");
        connector.Capabilities.Select(capability => capability.Category).Should().Contain(new[]
        {
            PartnerServiceCategory.Payout,
            PartnerServiceCategory.Collection,
            PartnerServiceCategory.BillPayment,
            PartnerServiceCategory.AirtimeTopup,
        });
    }

    [Fact]
    public async Task InitiatePayoutAsync_Should_ReturnProviderReference_And_Succeeded()
    {
        var connector = CreateConnector();
        var instruction = new PayoutInstruction(
            ClientReference: "PAYOUT-1",
            Amount: new Money(10000m, "NGN"),
            DebitCurrency: "NGN",
            Destination: new BankAccountDestination("044", "0690000031", null, "Simulated Holder"),
            Narration: "Test payout",
            CallbackUrl: null,
            Metadata: null);

        var result = await connector.InitiatePayoutAsync(instruction);

        result.Status.Should().Be(PartnerTransactionStatus.Succeeded);
        result.Reference.ClientReference.Should().Be("PAYOUT-1");
        result.Reference.ProviderReference.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ValidateThenPay_Should_RoundTrip_ValidationToken()
    {
        var connector = CreateConnector();

        var validation = await connector.ValidateCustomerAsync(new BillCustomerValidationRequest(
            ClientReference: "BILL-1",
            BillerCode: "SIM-ELEC",
            ItemCode: "SIM-ELEC-PREPAID",
            CustomerId: "1234567890",
            Inputs: null));

        validation.IsValid.Should().BeTrue();
        validation.ValidationToken.Should().NotBeNullOrEmpty();

        var payment = await connector.PayBillAsync(new BillPaymentInstruction(
            ClientReference: "BILL-1",
            BillerCode: "SIM-ELEC",
            ItemCode: "SIM-ELEC-PREPAID",
            CustomerId: "1234567890",
            Amount: new Money(5000m, "NGN"),
            ValidationToken: validation.ValidationToken,
            ServiceCategory: PartnerServiceCategory.BillPayment,
            Inputs: null));

        payment.Status.Should().Be(PartnerTransactionStatus.Succeeded);
        payment.Token.Should().NotBeNullOrEmpty();
        // The token returned by validate is accepted by, and echoed through, pay.
        payment.Raw.PayloadJson.Should().Contain(validation.ValidationToken);
    }

    [Fact]
    public async Task PayBillAsync_Airtime_Should_Succeed_WithoutPriorValidation()
    {
        var connector = CreateConnector();

        var payment = await connector.PayBillAsync(new BillPaymentInstruction(
            ClientReference: "AIRTIME-1",
            BillerCode: "SIM-TELCO",
            ItemCode: "SIM-AIRTIME",
            CustomerId: "08030000000",
            Amount: new Money(1000m, "NGN"),
            ValidationToken: null,
            ServiceCategory: PartnerServiceCategory.AirtimeTopup,
            Inputs: null));

        payment.Status.Should().Be(PartnerTransactionStatus.Succeeded);
        payment.Token.Should().NotBeNullOrEmpty();
        connector.Capabilities.Should().Contain(capability =>
            capability.Category == PartnerServiceCategory.AirtimeTopup);
    }

    [Fact]
    public void ResolvePayoutConnector_Should_BeCaseInsensitive_And_ThrowForUnknown()
    {
        var connector = CreateConnector();
        var resolver = CreateResolver(connector);

        resolver.ResolvePayoutConnector("simulated").Should().BeSameAs(connector);

        var act = () => resolver.ResolveBillPaymentConnector("does-not-exist");
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void TryResolvePayoutConnector_Should_ReturnConnector_When_QuerySatisfiable()
    {
        var connector = CreateConnector();
        var resolver = CreateResolver(connector);

        var satisfiable = resolver.TryResolvePayoutConnector(
            new PartnerConnectorQuery(PartnerServiceCategory.Payout, "NG", "NGN", "Bank"),
            out var resolved);

        satisfiable.Should().BeTrue();
        resolved.Should().BeSameAs(connector);

        var unsatisfiable = resolver.TryResolvePayoutConnector(
            new PartnerConnectorQuery(PartnerServiceCategory.Payout, "US", "USD", "Bank"),
            out var none);

        unsatisfiable.Should().BeFalse();
        none.Should().BeNull();
    }

    [Fact]
    public void TryResolveBillPaymentConnector_Should_Resolve_AirtimeTopupCategory()
    {
        var connector = CreateConnector();
        var resolver = CreateResolver(connector);

        var resolved = resolver.TryResolveBillPaymentConnector(
            new PartnerConnectorQuery(PartnerServiceCategory.AirtimeTopup, "NG", "NGN", null),
            out var airtime);

        resolved.Should().BeTrue();
        airtime.Should().BeSameAs(connector);
    }
}
