using Aonik.Finance.Contracts.Services.Partners.Connectors;
using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Services.Partners.Connectors;

/// <summary>
/// Reference connector that proves the partner abstraction compiles and routes end to end
/// without any network, secret, or vendor dependency. It implements all three port interfaces
/// on one object (mirroring StripeSimulatedPaymentProviderGateway), advertises a capability lane
/// per service category (including an AirtimeTopup lane), and returns deterministic fake
/// references with Succeeded / RequiresAction outcomes.
/// </summary>
internal sealed class SimulatedPartnerConnector
    : IPartnerPayoutConnector, IPartnerCollectionConnector, IPartnerBillPaymentConnector
{
    public string ProviderCode => "Simulated";

    public IReadOnlyCollection<PartnerConnectorCapability> Capabilities { get; } = new[]
    {
        new PartnerConnectorCapability(
            PartnerServiceCategory.Payout, new[] { "NG" }, new[] { "NGN" }, new[] { "Bank", "MobileMoney" }),
        new PartnerConnectorCapability(
            PartnerServiceCategory.Collection, new[] { "NG" }, new[] { "NGN" }, new[] { "Card" }),
        new PartnerConnectorCapability(
            PartnerServiceCategory.BillPayment, new[] { "NG" }, new[] { "NGN" }, new[] { "Bill" }),
        new PartnerConnectorCapability(
            PartnerServiceCategory.AirtimeTopup, new[] { "NG" }, new[] { "NGN" }, new[] { "Airtime", "Data" }),
    };

    // ── Payout ───────────────────────────────────────────────────────────────
    public Task<PayoutInitiationResult> InitiatePayoutAsync(
        PayoutInstruction instruction, CancellationToken cancellationToken = default)
    {
        var reference = BuildReference(instruction.ClientReference, "tr");
        var fee = new Money(Math.Round(instruction.Amount.Amount * 0.01m, 2), instruction.Amount.Currency);
        var result = new PayoutInitiationResult(
            reference, PartnerTransactionStatus.Succeeded, fee, Raw("00", "Successful"));
        return Task.FromResult(result);
    }

    public Task<PayoutStatusResult> GetPayoutStatusAsync(
        PartnerReference reference, CancellationToken cancellationToken = default)
        => Task.FromResult(new PayoutStatusResult(
            reference, PartnerTransactionStatus.Succeeded, null, Raw("00", "Successful")));

    public Task<PayoutQuoteResult> QuotePayoutAsync(
        PayoutQuoteRequest request, CancellationToken cancellationToken = default)
    {
        var fee = new Money(Math.Round(request.Amount.Amount * 0.01m, 2), request.Amount.Currency);
        var sameCurrency = string.Equals(
            request.Amount.Currency, request.DestinationCurrency, StringComparison.OrdinalIgnoreCase);
        decimal? fxRate = sameCurrency ? null : 1.0m;
        Money? converted = sameCurrency ? null : new Money(request.Amount.Amount, request.DestinationCurrency);
        return Task.FromResult(new PayoutQuoteResult(fee, fxRate, converted, Raw("00", "Quote")));
    }

    public Task<AccountResolutionResult> ResolveAccountAsync(
        AccountResolutionRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(new AccountResolutionResult(
            true, "SIMULATED ACCOUNT HOLDER", Raw("00", "Resolved")));

    // ── Collection ───────────────────────────────────────────────────────────
    public Task<CollectionInitiationResult> InitiateCollectionAsync(
        CollectionInstruction instruction, CancellationToken cancellationToken = default)
    {
        var reference = BuildReference(instruction.ClientReference, "co");
        var (status, nextAction) = instruction.Method switch
        {
            CardCollection card => (
                PartnerTransactionStatus.RequiresAction,
                new PartnerAuthorizationAction(
                    "redirect",
                    card.RedirectUrl ?? $"https://simulated.aonik.test/checkout/{reference.ProviderReference}",
                    null,
                    reference.ProviderReference)),
            UssdCollection ussd => (
                PartnerTransactionStatus.RequiresAction,
                new PartnerAuthorizationAction("ussd", null, $"*{ussd.BankCode}*000#", reference.ProviderReference)),
            _ => (PartnerTransactionStatus.Pending, (PartnerAuthorizationAction?)null),
        };
        return Task.FromResult(new CollectionInitiationResult(reference, status, nextAction, Raw("00", "Initiated")));
    }

    public Task<CollectionStatusResult> GetCollectionStatusAsync(
        PartnerReference reference, CancellationToken cancellationToken = default)
        => Task.FromResult(new CollectionStatusResult(
            reference, PartnerTransactionStatus.Succeeded, null, Raw("00", "Successful")));

    public Task<CollectionRefundResult> RefundCollectionAsync(
        CollectionRefundInstruction instruction, CancellationToken cancellationToken = default)
    {
        var reference = BuildReference(instruction.OriginalReference.ClientReference, "rf");
        return Task.FromResult(new CollectionRefundResult(
            reference, PartnerTransactionStatus.Succeeded, Raw("00", "Refunded")));
    }

    // ── Bill payment / airtime top-up ─────────────────────────────────────────
    public Task<IReadOnlyList<BillerCatalogEntry>> GetBillerCatalogAsync(
        BillerCatalogQuery query, CancellationToken cancellationToken = default)
    {
        const string ngn = "NGN";
        var entries = new List<BillerCatalogEntry>
        {
            new(
                "SIM-ELEC", "Simulated Electricity", "UTILITY", "Utilities",
                PartnerServiceCategory.BillPayment,
                new List<BillCustomerField> { new("meterNumber", "Meter Number", true) },
                new List<BillItem>
                {
                    new("SIM-ELEC-PREPAID", "Prepaid Electricity", BillAmountType.Variable,
                        null, new Money(100m, ngn), new Money(50000m, ngn)),
                }),
            new(
                "SIM-TELCO", "Simulated Telco", "AIRTIME", "Airtime & Data",
                PartnerServiceCategory.AirtimeTopup,
                new List<BillCustomerField> { new("phoneNumber", "Phone Number", true) },
                new List<BillItem>
                {
                    new("SIM-AIRTIME", "Airtime Top-up", BillAmountType.Variable,
                        null, new Money(50m, ngn), new Money(20000m, ngn)),
                    new("SIM-DATA-1GB", "1GB Data Bundle", BillAmountType.Fixed,
                        new Money(500m, ngn), null, null),
                }),
        };

        IReadOnlyList<BillerCatalogEntry> filtered = string.IsNullOrWhiteSpace(query.CategoryCode)
            ? entries
            : entries
                .Where(entry => string.Equals(entry.CategoryCode, query.CategoryCode, StringComparison.OrdinalIgnoreCase))
                .ToList();

        return Task.FromResult(filtered);
    }

    public Task<BillCustomerValidationResult> ValidateCustomerAsync(
        BillCustomerValidationRequest request, CancellationToken cancellationToken = default)
    {
        var token = $"vt_{Guid.NewGuid().ToString("N")[..16]}";
        var result = new BillCustomerValidationResult(
            IsValid: true,
            ValidationToken: token,
            CustomerName: "SIMULATED CUSTOMER",
            ResolvedFields: new Dictionary<string, string> { ["customerId"] = request.CustomerId },
            OutstandingAmount: null,
            Raw: Raw("00", "Validated"));
        return Task.FromResult(result);
    }

    public Task<BillPaymentResult> PayBillAsync(
        BillPaymentInstruction instruction, CancellationToken cancellationToken = default)
    {
        var reference = BuildReference(instruction.ClientReference, "bp");
        var isAirtime = instruction.ServiceCategory == PartnerServiceCategory.AirtimeTopup;
        var vendToken = isAirtime
            ? $"PIN-{Guid.NewGuid().ToString("N")[..12]}"
            : $"TKN-{Guid.NewGuid().ToString("N")[..12]}";

        // Carry the validation token through to the raw payload so a validate -> pay round-trip is observable.
        var payloadJson =
            $"{{\"validationToken\":\"{instruction.ValidationToken}\",\"customerId\":\"{instruction.CustomerId}\"}}";

        var result = new BillPaymentResult(
            reference, PartnerTransactionStatus.Succeeded, vendToken, Raw("00", "Paid", payloadJson));
        return Task.FromResult(result);
    }

    public Task<BillPaymentStatusResult> GetBillPaymentStatusAsync(
        PartnerReference reference, CancellationToken cancellationToken = default)
        => Task.FromResult(new BillPaymentStatusResult(
            reference, PartnerTransactionStatus.Succeeded, null, Raw("00", "Successful")));

    // ── Helpers ──────────────────────────────────────────────────────────────
    private static PartnerReference BuildReference(string clientReference, string prefix)
    {
        var suffix = Guid.NewGuid().ToString("N")[..16];
        return new PartnerReference(clientReference, $"{prefix}_{suffix}");
    }

    private static RawProviderResponse Raw(string code, string message, string? payloadJson = null)
        => new(code, message, payloadJson);
}
