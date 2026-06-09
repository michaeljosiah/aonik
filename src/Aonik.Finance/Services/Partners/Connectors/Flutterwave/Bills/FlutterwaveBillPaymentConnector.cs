using System.Globalization;
using System.Text;

using Aonik.Finance.Contracts.Services.Partners.Connectors;
using Aonik.Finance.Services.Partners.Connectors.Flutterwave.Bills.Dtos;
using Aonik.Finance.Services.Partners.Connectors.Flutterwave.Dtos;
using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Services.Partners.Connectors.Flutterwave.Bills;

/// <summary>
/// Flutterwave <strong>v3</strong> Bills connector (Spec 040). Shares the <c>ProviderCode</c>
/// "Flutterwave" with the v4 payout connector — safe because the resolver keeps a separate list per
/// port (§6.4). <see cref="GetBillerCatalogAsync"/> is the import read; the pay-path methods are
/// implemented so the port is whole (Phase 2) but are not exercised by import. NG-only: a non-NG query
/// returns empty without calling the API. Fails closed when not configured.
/// </summary>
internal sealed class FlutterwaveBillPaymentConnector : IPartnerBillPaymentConnector
{
    private const string Ngn = "NGN";

    private readonly FlutterwaveBillsClient _client;
    private readonly IFlutterwaveBillsConfigProvider _configProvider;

    public FlutterwaveBillPaymentConnector(
        FlutterwaveBillsClient client,
        IFlutterwaveBillsConfigProvider configProvider)
    {
        _client = client;
        _configProvider = configProvider;
    }

    public string ProviderCode => "Flutterwave";

    public IReadOnlyCollection<PartnerConnectorCapability> Capabilities { get; } = new[]
    {
        new PartnerConnectorCapability(
            PartnerServiceCategory.BillPayment, new[] { "NG" }, new[] { "NGN" }, new[] { "Bill" }),
        new PartnerConnectorCapability(
            PartnerServiceCategory.AirtimeTopup, new[] { "NG" }, new[] { "NGN" }, new[] { "Airtime", "Data" }),
    };

    // ── Catalogue read (import) ───────────────────────────────────────────────
    public async Task<IReadOnlyList<BillerCatalogEntry>> GetBillerCatalogAsync(
        BillerCatalogQuery query, CancellationToken cancellationToken = default)
    {
        var options = await GetConfiguredOptionsAsync(cancellationToken);

        var country = string.IsNullOrWhiteSpace(query.Country)
            ? options.Country
            : query.Country.Trim().ToUpperInvariant();

        // Flutterwave Bills is NG-only (§3) — return empty rather than pretend breadth we cannot serve.
        if (!string.Equals(country, "NG", StringComparison.OrdinalIgnoreCase))
        {
            return Array.Empty<BillerCatalogEntry>();
        }

        var envelope = await _client.GetAsync<FwEnvelope<List<FwBillCategoryRow>>>(
            $"top-bill-categories?country={country}", cancellationToken);
        var rows = envelope.Data ?? new List<FwBillCategoryRow>();

        var entries = rows
            .Where(row => !string.IsNullOrWhiteSpace(row.BillerCode))
            .GroupBy(row => row.BillerCode!, StringComparer.OrdinalIgnoreCase)
            .Select(group => BuildEntry(group.Key, group.ToList()))
            .ToList();

        // Category filter is applied client-side (v3's category param is coarse — §6.2).
        if (!string.IsNullOrWhiteSpace(query.CategoryCode))
        {
            entries = entries
                .Where(entry => string.Equals(entry.CategoryCode, query.CategoryCode, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        // Selection filter: import re-reads only the operator's chosen billers (O2).
        if (query.BillerCodes is { Count: > 0 })
        {
            var selected = new HashSet<string>(query.BillerCodes, StringComparer.OrdinalIgnoreCase);
            entries = entries.Where(entry => selected.Contains(entry.BillerCode)).ToList();
        }

        // Lazy expansion: pull the full product list only when asked (import), bounding API calls.
        if (query.ExpandItems)
        {
            var expanded = new List<BillerCatalogEntry>(entries.Count);
            foreach (var entry in entries)
            {
                var items = await ExpandProductsAsync(entry, cancellationToken);
                expanded.Add(entry with { Items = items });
            }

            entries = expanded;
        }

        return entries;
    }

    private static BillerCatalogEntry BuildEntry(string billerCode, List<FwBillCategoryRow> rows)
    {
        var first = rows[0];
        var isAirtime = rows.Any(row => row.IsAirtime == true) || IsAirtimeCategory(first.BillerName);
        var serviceCategory = isAirtime
            ? PartnerServiceCategory.AirtimeTopup
            : PartnerServiceCategory.BillPayment;

        var categoryCode = string.IsNullOrWhiteSpace(first.BillerName)
            ? "BILLS"
            : first.BillerName.Trim().ToUpperInvariant();
        var categoryName = string.IsNullOrWhiteSpace(first.BillerName) ? "Bills" : ToTitle(first.BillerName);
        var billerName = FirstNonBlank(first.Name, first.ShortName, billerCode);
        var fields = BuildFields(first.LabelName);

        // Dense items: one BillItem per distinct category row (amount unknown at this depth → Variable).
        var items = rows
            .Where(row => !string.IsNullOrWhiteSpace(row.ItemCode))
            .Select(row => new BillItem(
                row.ItemCode!, FirstNonBlank(row.Name, row.ShortName, row.ItemCode!),
                BillAmountType.Variable, null, null, null))
            .GroupBy(item => item.ItemCode, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        if (items.Count == 0)
        {
            items.Add(new BillItem(billerCode, billerName, BillAmountType.Variable, null, null, null));
        }

        return new BillerCatalogEntry(
            billerCode, billerName, categoryCode, categoryName, serviceCategory, fields, items);
    }

    private async Task<IReadOnlyList<BillItem>> ExpandProductsAsync(
        BillerCatalogEntry entry, CancellationToken cancellationToken)
    {
        var envelope = await _client.GetAsync<FwEnvelope<FwBillerProductsData>>(
            $"billers/{entry.BillerCode}/items", cancellationToken);
        var products = envelope.Data?.Products;
        if (products is null || products.Count == 0)
        {
            return entry.Items;
        }

        return products
            .Where(product => !string.IsNullOrWhiteSpace(product.Code))
            .Select(product =>
            {
                var amount = ParseAmount(product.Amount);
                var amountType = amount is > 0m ? BillAmountType.Fixed : BillAmountType.Variable;
                Money? fixedAmount = amountType == BillAmountType.Fixed ? new Money(amount!.Value, Ngn) : null;
                return new BillItem(
                    product.Code!, FirstNonBlank(product.Name, product.Code!),
                    amountType, fixedAmount, null, null);
            })
            .GroupBy(item => item.ItemCode, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    // ── Pay path (Phase 2 — implemented so the port is whole; not used by import) ──
    public async Task<BillCustomerValidationResult> ValidateCustomerAsync(
        BillCustomerValidationRequest request, CancellationToken cancellationToken = default)
    {
        await GetConfiguredOptionsAsync(cancellationToken);

        // Verify exact verb/path against the live reference (O6) — validate is documented as both GET
        // and POST; the Node SDK uses POST with this body.
        var body = new { item_code = request.ItemCode, code = request.BillerCode, customer = request.CustomerId };
        var envelope = await _client.PostAsync<FwEnvelope<FwBillValidateData>>(
            $"bill-items/{request.ItemCode}/validate", body, cancellationToken);
        var data = envelope.Data;

        var isValid = string.Equals(data?.ResponseCode, "00", StringComparison.Ordinal);
        Money? outstanding = data?.Maximum is > 0m ? new Money(data.Maximum.Value, Ngn) : null;
        return new BillCustomerValidationResult(
            isValid,
            data?.ProductCode,
            data?.Name,
            ResolvedFields: null,
            outstanding,
            new RawProviderResponse(data?.ResponseCode, data?.ResponseMessage, null));
    }

    public async Task<BillPaymentResult> PayBillAsync(
        BillPaymentInstruction instruction, CancellationToken cancellationToken = default)
    {
        var options = await GetConfiguredOptionsAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(instruction.BillerCode) || string.IsNullOrWhiteSpace(instruction.ItemCode))
        {
            throw new FlutterwaveException(
                "Bill payment requires both a biller code and an item code to target the correct product.",
                errorType: "NO_ITEM",
                errorCode: null,
                statusCode: null,
                retryable: false);
        }

        var reference = FlutterwaveReferences.SanitizeReference(instruction.ClientReference);

        // Body fields per Flutterwave v3 "Create a bill payment" (the item-scoped endpoint): the customer
        // identifier is customer_id — NOT customer, which only the generic POST /bills uses — and there is
        // no recurrence parameter here. Sending customer would be treated as a missing customer id and the
        // payment rejected. https://developer.flutterwave.com/v3.0/reference/create-a-bill-payment
        var body = new
        {
            country = options.Country,
            customer_id = instruction.CustomerId,
            amount = instruction.Amount.Amount,
            reference
        };

        // Target the specific biller item (Spec 040 §5.4 / O6). The generic POST /bills + type cannot
        // disambiguate item-based billers (fixed data bundles, cable packages), so we always post to the
        // item-scoped path keyed by the validated biller_code + item_code carried on the instruction —
        // the same codes catalogue import and validation use. Verify the exact path in sandbox (O6).
        var path = $"billers/{Uri.EscapeDataString(instruction.BillerCode)}/items/{Uri.EscapeDataString(instruction.ItemCode)}/payment";
        var envelope = await _client.PostAsync<FwEnvelope<FwBillPayData>>(path, body, cancellationToken);
        var status = MapStatus(envelope.Status);

        // Status reconciliation (GET /v3/bills/{reference}) keys off tx_ref, NOT flw_ref — bill-payment
        // responses may omit flw_ref entirely, so storing it would leave the provider reference null and
        // status polling would miss the transaction. Use tx_ref, falling back to the reference we
        // submitted (which Flutterwave tracks as the tx_ref).
        var providerReference = envelope.Data?.TxRef ?? reference;
        var partnerReference = new PartnerReference(instruction.ClientReference, providerReference);
        return new BillPaymentResult(
            partnerReference, status, Token: null,
            new RawProviderResponse(null, envelope.Message, null));
    }

    public async Task<BillPaymentStatusResult> GetBillPaymentStatusAsync(
        PartnerReference reference, CancellationToken cancellationToken = default)
    {
        await GetConfiguredOptionsAsync(cancellationToken);

        var lookup = reference.ProviderReference ?? reference.ClientReference;
        var envelope = await _client.GetAsync<FwEnvelope<FwBillStatusData>>($"bills/{lookup}", cancellationToken);
        var status = MapStatus(envelope.Status);
        return new BillPaymentStatusResult(
            reference, status, Token: null,
            new RawProviderResponse(null, envelope.Message, envelope.Data?.TxRef));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private async Task<FlutterwaveBillsOptions> GetConfiguredOptionsAsync(CancellationToken cancellationToken)
    {
        var options = await _configProvider.GetAsync(cancellationToken);
        if (!options.IsConfigured())
        {
            throw new FlutterwaveException(
                "Flutterwave bills is not configured or disabled.",
                errorType: "CONFIGURATION",
                errorCode: null,
                statusCode: null,
                retryable: false);
        }

        return options;
    }

    private static PartnerTransactionStatus MapStatus(string? status)
        => string.Equals(status, "success", StringComparison.OrdinalIgnoreCase)
            ? PartnerTransactionStatus.Succeeded
            : PartnerTransactionStatus.Pending;

    private static bool IsAirtimeCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return false;
        }

        return category.Contains("airtime", StringComparison.OrdinalIgnoreCase)
            || category.Contains("data", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<BillCustomerField> BuildFields(string? labelName)
    {
        var label = string.IsNullOrWhiteSpace(labelName) ? "Customer ID" : labelName.Trim();
        var key = ToCamelKey(label);
        return new List<BillCustomerField> { new(key, label, true) };
    }

    private static decimal? ParseAmount(string? raw)
        => decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var value) ? value : null;

    private static string FirstNonBlank(params string?[] candidates)
        => candidates.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private static string ToTitle(string value)
        => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(value.Trim().ToLowerInvariant());

    private static string ToCamelKey(string label)
    {
        var words = label.Split(new[] { ' ', '_', '-', '/' }, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return "customer";
        }

        var builder = new StringBuilder();
        for (var i = 0; i < words.Length; i++)
        {
            var word = new string(words[i].Where(char.IsLetterOrDigit).ToArray());
            if (word.Length == 0)
            {
                continue;
            }

            if (builder.Length == 0)
            {
                builder.Append(char.ToLowerInvariant(word[0])).Append(word[1..]);
            }
            else
            {
                builder.Append(char.ToUpperInvariant(word[0])).Append(word[1..]);
            }
        }

        return builder.Length == 0 ? "customer" : builder.ToString();
    }
}
