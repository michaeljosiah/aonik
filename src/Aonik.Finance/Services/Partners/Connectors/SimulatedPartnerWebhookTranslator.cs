using System.Text.Json;

using Aonik.Finance.Contracts.Services.Partners.Connectors;

namespace Aonik.Finance.Services.Partners.Connectors;

/// <summary>
/// Per-provider translator for the simulated connector. Verifies a shared-secret header and lifts
/// a flat JSON envelope into the normalized <see cref="PartnerWebhookEvent"/>. A real vendor adds
/// its own translator implementation keyed by <see cref="ProviderCode"/>; nothing else changes.
/// </summary>
internal sealed class SimulatedPartnerWebhookTranslator : IPartnerWebhookTranslator
{
    public string ProviderCode => "Simulated";

    public bool VerifySignature(PartnerWebhookEnvelope envelope, string signingSecret)
    {
        if (string.IsNullOrEmpty(signingSecret))
        {
            return false;
        }

        return envelope.Headers.TryGetValue("x-simulated-signature", out var signature)
            && string.Equals(signature, signingSecret, StringComparison.Ordinal);
    }

    public PartnerWebhookEvent Translate(PartnerWebhookEnvelope envelope)
    {
        // The simulated payload is flat JSON:
        // { category, event, clientReference, providerReference, status, code, message }
        using var document = JsonDocument.Parse(
            string.IsNullOrWhiteSpace(envelope.Body) ? "{}" : envelope.Body);
        var root = document.RootElement;

        var category = ParseCategory(GetString(root, "category"));
        var eventType = GetString(root, "event") ?? "unknown";
        var clientReference = GetString(root, "clientReference") ?? string.Empty;
        var providerReference = GetString(root, "providerReference");
        var status = ParseStatus(GetString(root, "status"));

        return new PartnerWebhookEvent(
            category,
            eventType,
            new PartnerReference(clientReference, providerReference),
            status,
            new RawProviderResponse(GetString(root, "code"), GetString(root, "message"), envelope.Body));
    }

    private static string? GetString(JsonElement root, string property)
        => root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static PartnerServiceCategory ParseCategory(string? value)
        => Enum.TryParse<PartnerServiceCategory>(value, ignoreCase: true, out var category)
            ? category
            : PartnerServiceCategory.Collection;

    private static PartnerTransactionStatus ParseStatus(string? value)
        => Enum.TryParse<PartnerTransactionStatus>(value, ignoreCase: true, out var status)
            ? status
            : PartnerTransactionStatus.Unknown;
}
