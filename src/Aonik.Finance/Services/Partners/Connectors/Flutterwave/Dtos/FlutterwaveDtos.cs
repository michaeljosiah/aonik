using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aonik.Finance.Services.Partners.Connectors.Flutterwave.Dtos;

/// <summary>Shared JSON options for Flutterwave wire models (snake_case via explicit attributes).</summary>
internal static class FlutterwaveJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}

// ── OAuth ────────────────────────────────────────────────────────────────────
internal sealed class FwTokenResponse
{
    [JsonPropertyName("access_token")] public string? AccessToken { get; set; }
    [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
    [JsonPropertyName("token_type")] public string? TokenType { get; set; }
}

// ── Errors + envelope ────────────────────────────────────────────────────────
internal sealed class FwErrorEnvelope
{
    [JsonPropertyName("status")] public string? Status { get; set; }
    [JsonPropertyName("error")] public FwError? Error { get; set; }
}

internal sealed class FwError
{
    [JsonPropertyName("type")] public string? Type { get; set; }
    [JsonPropertyName("code")] public string? Code { get; set; }
    [JsonPropertyName("message")] public string? Message { get; set; }
}

internal sealed class FwEnvelope<T>
{
    [JsonPropertyName("status")] public string? Status { get; set; }
    [JsonPropertyName("message")] public string? Message { get; set; }
    [JsonPropertyName("data")] public T? Data { get; set; }
}

// ── Account resolve (name enquiry) ───────────────────────────────────────────
internal sealed class FwAccountResolveRequest
{
    [JsonPropertyName("currency")] public string Currency { get; set; } = string.Empty;
    [JsonPropertyName("account")] public FwAccountRef Account { get; set; } = new();
}

internal sealed class FwAccountRef
{
    [JsonPropertyName("code")] public string Code { get; set; } = string.Empty;
    [JsonPropertyName("number")] public string Number { get; set; } = string.Empty;
}

internal sealed class FwAccountResolveData
{
    [JsonPropertyName("bank_code")] public string? BankCode { get; set; }
    [JsonPropertyName("account_number")] public string? AccountNumber { get; set; }
    [JsonPropertyName("account_name")] public string? AccountName { get; set; }
}

// ── Rates (quote) ────────────────────────────────────────────────────────────
internal sealed class FwRateRequest
{
    [JsonPropertyName("source")] public FwRateCurrency Source { get; set; } = new();
    [JsonPropertyName("destination")] public FwRateCurrency Destination { get; set; } = new();
    [JsonPropertyName("precision")] public int? Precision { get; set; }
}

internal sealed class FwRateCurrency
{
    [JsonPropertyName("currency")] public string Currency { get; set; } = string.Empty;
    [JsonPropertyName("amount")] public decimal? Amount { get; set; }
}

internal sealed class FwRateData
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    // Flutterwave returns the rate as a string in the documented examples.
    [JsonPropertyName("rate")] public string? Rate { get; set; }
    [JsonPropertyName("source")] public FwRateAmount? Source { get; set; }
    [JsonPropertyName("destination")] public FwRateAmount? Destination { get; set; }
}

internal sealed class FwRateAmount
{
    [JsonPropertyName("amount")] public string? Amount { get; set; }
    [JsonPropertyName("currency")] public string? Currency { get; set; }
}

// ── Transfer recipients ──────────────────────────────────────────────────────
internal sealed class FwRecipientRequest
{
    [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
    [JsonPropertyName("name")] public FwName? Name { get; set; }
    [JsonPropertyName("bank")] public FwRecipientBank? Bank { get; set; }
    [JsonPropertyName("mobile_money")] public FwRecipientMobileMoney? MobileMoney { get; set; }
    [JsonPropertyName("wallet")] public FwRecipientWallet? Wallet { get; set; }
}

internal sealed class FwName
{
    [JsonPropertyName("first")] public string? First { get; set; }
    [JsonPropertyName("last")] public string? Last { get; set; }
}

internal sealed class FwRecipientBank
{
    [JsonPropertyName("code")] public string Code { get; set; } = string.Empty;
    [JsonPropertyName("account_number")] public string AccountNumber { get; set; } = string.Empty;
}

internal sealed class FwRecipientMobileMoney
{
    [JsonPropertyName("network")] public string Network { get; set; } = string.Empty;
    [JsonPropertyName("msisdn")] public string Msisdn { get; set; } = string.Empty;
    [JsonPropertyName("country")] public string? Country { get; set; }
}

internal sealed class FwRecipientWallet
{
    [JsonPropertyName("provider")] public string Provider { get; set; } = "flutterwave";
    [JsonPropertyName("identifier")] public string Identifier { get; set; } = string.Empty;
}

internal sealed class FwRecipientData
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("type")] public string? Type { get; set; }
}

// ── Transfers (orchestrator, via recipient_id) ───────────────────────────────
internal sealed class FwTransferRequest
{
    [JsonPropertyName("action")] public string Action { get; set; } = "instant";
    [JsonPropertyName("reference")] public string Reference { get; set; } = string.Empty;
    [JsonPropertyName("narration")] public string? Narration { get; set; }
    [JsonPropertyName("transfer_purpose")] public string TransferPurpose { get; set; } = string.Empty;
    [JsonPropertyName("payment_instruction")] public FwPaymentInstruction PaymentInstruction { get; set; } = new();
    [JsonPropertyName("meta")] public Dictionary<string, string>? Meta { get; set; }
}

internal sealed class FwPaymentInstruction
{
    [JsonPropertyName("source_currency")] public string SourceCurrency { get; set; } = string.Empty;
    [JsonPropertyName("destination_currency")] public string DestinationCurrency { get; set; } = string.Empty;
    [JsonPropertyName("amount")] public FwAmount Amount { get; set; } = new();
    [JsonPropertyName("recipient_id")] public string RecipientId { get; set; } = string.Empty;
}

internal sealed class FwAmount
{
    [JsonPropertyName("value")] public decimal Value { get; set; }
    [JsonPropertyName("applies_to")] public string AppliesTo { get; set; } = "destination_currency";
}

internal sealed class FwTransferData
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("reference")] public string? Reference { get; set; }
    [JsonPropertyName("status")] public string? Status { get; set; }
    [JsonPropertyName("fee")] public FwMoney? Fee { get; set; }
    [JsonPropertyName("debit_information")] public FwDebitInformation? DebitInformation { get; set; }
    [JsonPropertyName("provider_response")] public FwProviderResponse? ProviderResponse { get; set; }
}

internal sealed class FwMoney
{
    [JsonPropertyName("currency")] public string? Currency { get; set; }
    [JsonPropertyName("value")] public decimal? Value { get; set; }
}

internal sealed class FwDebitInformation
{
    [JsonPropertyName("currency")] public string? Currency { get; set; }
    [JsonPropertyName("actual_debit_amount")] public decimal? ActualDebitAmount { get; set; }
    [JsonPropertyName("rate_used")] public decimal? RateUsed { get; set; }
    [JsonPropertyName("vat")] public decimal? Vat { get; set; }
}

internal sealed class FwProviderResponse
{
    [JsonPropertyName("type")] public string? Type { get; set; }
    [JsonPropertyName("code")] public string? Code { get; set; }
    [JsonPropertyName("message")] public string? Message { get; set; }
}

// ── Webhook payload ──────────────────────────────────────────────────────────
internal sealed class FwWebhookEnvelope
{
    [JsonPropertyName("type")] public string? Type { get; set; }
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("data")] public FwWebhookData? Data { get; set; }
}

internal sealed class FwWebhookData
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("reference")] public string? Reference { get; set; }
    [JsonPropertyName("status")] public string? Status { get; set; }
    [JsonPropertyName("meta")] public Dictionary<string, JsonElement>? Meta { get; set; }
}
