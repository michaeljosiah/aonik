using System.Text.Json.Serialization;

namespace Aonik.Finance.Services.Partners.Connectors.Flutterwave.Bills.Dtos;

// ── Catalogue read (Spec 040 §5.1 / §5.2) ─────────────────────────────────────

/// <summary>
/// A row from <c>GET /v3/top-bill-categories?country=NG</c> (and the fuller <c>/v3/bill-categories</c>,
/// same shape). Each row is a (biller, default item) pair: <see cref="BillerName"/> is the category
/// label (e.g. "AIRTIME"), <see cref="Name"/> the biller name (e.g. "MTN Nigeria").
/// </summary>
internal sealed class FwBillCategoryRow
{
    [JsonPropertyName("biller_code")] public string? BillerCode { get; set; }
    [JsonPropertyName("item_code")] public string? ItemCode { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("short_name")] public string? ShortName { get; set; }
    [JsonPropertyName("biller_name")] public string? BillerName { get; set; }
    [JsonPropertyName("is_airtime")] public bool? IsAirtime { get; set; }
    [JsonPropertyName("country")] public string? Country { get; set; }
    [JsonPropertyName("label_name")] public string? LabelName { get; set; }
}

/// <summary>Response data from <c>GET /v3/billers/{biller_code}/items</c> (a.k.a. products).</summary>
internal sealed class FwBillerProductsData
{
    [JsonPropertyName("biller_code")] public string? BillerCode { get; set; }
    [JsonPropertyName("products")] public List<FwBillerProduct>? Products { get; set; }
}

internal sealed class FwBillerProduct
{
    [JsonPropertyName("code")] public string? Code { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }

    // v3 returns amount/fee as strings (e.g. "0.0" for a variable-amount service).
    [JsonPropertyName("amount")] public string? Amount { get; set; }
    [JsonPropertyName("fee")] public string? Fee { get; set; }
    [JsonPropertyName("description")] public string? Description { get; set; }
}

// ── Pay path (Spec 040 §5.3–§5.5, Phase 2) ────────────────────────────────────

/// <summary>Response data from <c>POST /v3/bill-items/{item_code}/validate</c> (§5.3).</summary>
internal sealed class FwBillValidateData
{
    [JsonPropertyName("response_code")] public string? ResponseCode { get; set; }
    [JsonPropertyName("response_message")] public string? ResponseMessage { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("biller_code")] public string? BillerCode { get; set; }
    [JsonPropertyName("customer")] public string? Customer { get; set; }
    [JsonPropertyName("product_code")] public string? ProductCode { get; set; }
    [JsonPropertyName("fee")] public decimal? Fee { get; set; }
    [JsonPropertyName("maximum")] public decimal? Maximum { get; set; }
    [JsonPropertyName("minimum")] public decimal? Minimum { get; set; }
}

/// <summary>Response data from <c>POST /v3/bills</c> (§5.4).</summary>
internal sealed class FwBillPayData
{
    [JsonPropertyName("phone_number")] public string? PhoneNumber { get; set; }
    [JsonPropertyName("amount")] public decimal? Amount { get; set; }
    [JsonPropertyName("network")] public string? Network { get; set; }
    [JsonPropertyName("flw_ref")] public string? FlwRef { get; set; }
    [JsonPropertyName("tx_ref")] public string? TxRef { get; set; }
}

/// <summary>Response data from <c>GET /v3/bills/{reference}</c> (§5.5).</summary>
internal sealed class FwBillStatusData
{
    [JsonPropertyName("currency")] public string? Currency { get; set; }
    [JsonPropertyName("customer_id")] public string? CustomerId { get; set; }
    [JsonPropertyName("amount")] public string? Amount { get; set; }
    [JsonPropertyName("product")] public string? Product { get; set; }
    [JsonPropertyName("product_name")] public string? ProductName { get; set; }
    [JsonPropertyName("country")] public string? Country { get; set; }
    [JsonPropertyName("tx_ref")] public string? TxRef { get; set; }
}
