using System.Text;
using System.Text.Json;

using Google.Apis.Auth;
using Microsoft.Extensions.Options;

using Aonik.SharedKernel.Abstractions.Connectors;

namespace Aonik.Infrastructure.Integrations.Gmail;

internal sealed class GmailMailboxPushVerifier(GmailMailboxSyncProvider provider, IOptionsMonitor<GmailMailboxOptions> options) : IConnectorMailboxPushVerifier
{
    public async Task<VerifiedMailboxPush> VerifyAsync(string authorization, string envelopeJson, CancellationToken cancellationToken = default)
    {
        if (!provider.PushConfigured || authorization is not { Length: > 7 and <= 16_384 }
            || !authorization.StartsWith("Bearer ", StringComparison.Ordinal) || envelopeJson.Length > 32_768) throw Denied();
        try
        {
            GoogleJsonWebSignature.Payload claims = await GoogleJsonWebSignature.ValidateAsync(authorization[7..], new GoogleJsonWebSignature.ValidationSettings
            { Audience = [options.CurrentValue.PushAudience] }).WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            if (claims.Issuer is not ("accounts.google.com" or "https://accounts.google.com") || !claims.EmailVerified
                || claims.Email != options.CurrentValue.PushServiceAccount) throw Denied();
            using JsonDocument envelope = JsonDocument.Parse(envelopeJson, new() { MaxDepth = 8 });
            JsonElement root = envelope.RootElement;
            if (root.GetProperty("subscription").GetString() != provider.PushSubscription) throw Denied();
            JsonElement message = root.GetProperty("message");
            string id = message.GetProperty("messageId").GetString() ?? "";
            if (id.Length is < 1 or > 256 || !id.All(char.IsAsciiDigit)) throw Denied();
            string data = message.GetProperty("data").GetString() ?? "";
            if (data.Length > 4096) throw Denied();
            using JsonDocument notification = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(data)), new() { MaxDepth = 4 });
            string email = notification.RootElement.GetProperty("emailAddress").GetString() ?? "";
            if (email.Length is < 3 or > 254 || email.Any(char.IsControl) || !email.Contains('@')) throw Denied();
            return new(provider.ApplicationKey, provider.PushSubscription!, email, GmailMailboxSyncProvider.History(notification.RootElement.GetProperty("historyId").GetString() ?? ""), id);
        }
        catch (Exception exception) when (exception is InvalidJwtException or JsonException or KeyNotFoundException or InvalidOperationException
            or FormatException or TimeoutException or ConnectorProviderException) { throw Denied(); }
    }
    private static ConnectorAccessException Denied() => new("MailboxPushUnauthorized", "Mailbox push authentication failed.");
}
