using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Aonik.Finance.Contracts.Services.Partners.Connectors;
using Aonik.Finance.Services.Partners.Connectors.Flutterwave.Dtos;

namespace Aonik.Finance.Services.Partners.Connectors.Flutterwave;

/// <summary>
/// Flutterwave v4 webhook translator (Spec 037 §7.5). The endpoint + dedupe + settlement ship in
/// PR #147; this supplies only the <c>"Flutterwave"</c> translator that
/// <c>ProcessWebhookAsync</c> resolves by <see cref="ProviderCode"/>. Three invariants:
/// (1) <see cref="VerifySignature"/> is HMAC-SHA256 base64 over the UTF-8 bytes of
/// <c>envelope.Body</c>, with a case-insensitive <c>flutterwave-signature</c> lookup and a
/// constant-time compare; (2) <see cref="Translate"/> reconstructs the stored
/// <c>ClientReference</c> (<c>REM-{orderId:N}</c>) from the echoed <c>meta.aonik_client_reference</c>
/// so <c>LocateRemittancePayoutAsync</c> matches (G17); (3) <see cref="Translate"/> never throws on a
/// malformed body — it returns <see cref="PartnerTransactionStatus.Unknown"/> (G7 ordering, §4 item 4).
/// </summary>
internal sealed class FlutterwaveWebhookTranslator : IPartnerWebhookTranslator
{
    private const string SignatureHeader = "flutterwave-signature";
    private const string ReferenceMetaKey = "aonik_client_reference";

    public string ProviderCode => "Flutterwave";

    public bool VerifySignature(PartnerWebhookEnvelope envelope, string signingSecret)
    {
        if (string.IsNullOrEmpty(signingSecret))
        {
            return false;
        }

        var header = LookupHeader(envelope.Headers, SignatureHeader);
        if (header is null)
        {
            return false;
        }

        var computed = Convert.ToBase64String(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(signingSecret),
            Encoding.UTF8.GetBytes(envelope.Body ?? string.Empty)));

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computed),
            Encoding.UTF8.GetBytes(header));
    }

    public PartnerWebhookEvent Translate(PartnerWebhookEnvelope envelope)
    {
        try
        {
            var payload = JsonSerializer.Deserialize<FwWebhookEnvelope>(
                string.IsNullOrWhiteSpace(envelope.Body) ? "{}" : envelope.Body, FlutterwaveJson.Options);

            var eventType = payload?.Type ?? "unknown";
            var category = MapCategory(eventType);
            var status = MapStatus(payload?.Data?.Status);

            var clientReference = ResolveClientReference(payload?.Data) ?? string.Empty;
            var providerReference = payload?.Data?.Id;

            return new PartnerWebhookEvent(
                category,
                eventType,
                new PartnerReference(clientReference, providerReference),
                status,
                new RawProviderResponse(payload?.Data?.Status, null, null));
        }
        catch (JsonException)
        {
            // Non-throwing contract: a malformed/adversarial body must not 500 the shipped handler.
            return new PartnerWebhookEvent(
                PartnerServiceCategory.Payout,
                "unknown",
                new PartnerReference(string.Empty, null),
                PartnerTransactionStatus.Unknown,
                new RawProviderResponse(null, "unparseable", null));
        }
    }

    private static string? ResolveClientReference(FwWebhookData? data)
    {
        if (data is null)
        {
            return null;
        }

        // Prefer the echoed meta (our exact REM-{orderId:N}); fall back to the body reference (G17).
        if (data.Meta is not null
            && data.Meta.TryGetValue(ReferenceMetaKey, out var metaValue)
            && metaValue.ValueKind == JsonValueKind.String)
        {
            var value = metaValue.GetString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return data.Reference;
    }

    private static PartnerServiceCategory MapCategory(string eventType)
        => eventType.StartsWith("charge", StringComparison.OrdinalIgnoreCase)
            ? PartnerServiceCategory.Collection
            : PartnerServiceCategory.Payout;

    private static PartnerTransactionStatus MapStatus(string? status)
        => status?.Trim().ToUpperInvariant() switch
        {
            "SUCCESSFUL" or "SUCCEEDED" => PartnerTransactionStatus.Succeeded,
            "FAILED" => PartnerTransactionStatus.Failed,
            "CANCELLED" => PartnerTransactionStatus.Failed,
            "REVERSED" or "VOIDED" => PartnerTransactionStatus.Reversed,
            "PENDING" => PartnerTransactionStatus.Processing,
            "NEW" or "INITIATED" => PartnerTransactionStatus.Pending,
            _ => PartnerTransactionStatus.Unknown,
        };

    private static string? LookupHeader(IReadOnlyDictionary<string, string> headers, string name)
    {
        if (headers.TryGetValue(name, out var direct))
        {
            return direct;
        }

        foreach (var header in headers)
        {
            if (string.Equals(header.Key, name, StringComparison.OrdinalIgnoreCase))
            {
                return header.Value;
            }
        }

        return null;
    }
}
