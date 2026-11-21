using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using Microsoft.Extensions.Options;

using Aonik.SharedKernel.Abstractions.Connectors;

namespace Aonik.Infrastructure.Integrations.Gmail;

internal sealed class GmailMailboxSyncProvider(System.Net.Http.IHttpClientFactory clients,
    IOptionsMonitor<GmailConnectorOptions> oauth, IOptionsMonitor<GmailMailboxOptions> options) : IConnectorMailboxSyncProvider
{
    public string ConnectorKey => GmailActionKeys.Connector;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(oauth.CurrentValue.ClientId);
    public string ApplicationKey => Hash(oauth.CurrentValue.ClientId);
    public string? PushTopic => PushConfigured ? options.CurrentValue.PushTopic : null;
    public string? PushSubscription => PushConfigured ? options.CurrentValue.PushSubscription : null;
    internal bool PushConfigured => IsConfigured
        && Resource(options.CurrentValue.PushTopic, "topics") && Resource(options.CurrentValue.PushSubscription, "subscriptions")
        && Uri.TryCreate(options.CurrentValue.PushAudience, UriKind.Absolute, out Uri? audience) && audience.Scheme == "https"
        && string.IsNullOrEmpty(audience.UserInfo) && string.IsNullOrEmpty(audience.Fragment)
        && Regex.IsMatch(options.CurrentValue.PushServiceAccount, "^[a-zA-Z0-9._-]+@[a-zA-Z0-9.-]+\\.iam\\.gserviceaccount\\.com$", RegexOptions.NonBacktracking);

    public async Task<MailboxProfile> GetProfileAsync(MailboxProviderCall call, CancellationToken cancellationToken = default)
    {
        JsonElement json = await SendAsync(call, HttpMethod.Get, "profile?fields=emailAddress,historyId", null, cancellationToken);
        return new(Text(json, "emailAddress", 254), History(Text(json, "historyId", 40)));
    }

    public async Task<MailboxWatch> WatchAsync(MailboxProviderCall call, string topic, IReadOnlyList<string> labelIds, CancellationToken cancellationToken = default)
    {
        if (!PushConfigured || topic != PushTopic) throw new ConnectorProviderException("MailboxPushNotConfigured");
        ValidateLabels(labelIds);
        JsonElement json = await SendAsync(call, HttpMethod.Post, "watch", ConnectorJson.Serialize(new { topicName = topic,
            labelIds, labelFilterBehavior = "include" }), cancellationToken);
        if (!long.TryParse(Text(json, "expiration", 20), NumberStyles.None, CultureInfo.InvariantCulture, out long milliseconds)) throw Invalid();
        DateTime expires;
        try { expires = DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).UtcDateTime; }
        catch (ArgumentOutOfRangeException) { throw Invalid(); }
        return new(History(Text(json, "historyId", 40)), expires);
    }

    public async Task StopWatchAsync(MailboxProviderCall call, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = Request(call, HttpMethod.Post, "stop");
        using HttpResponseMessage response = await clients.CreateClient("Gmail").SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode) throw new ConnectorProviderException("MailboxStopFailed", true);
    }

    public async Task<MailboxHistoryPage> ReadHistoryAsync(MailboxProviderCall call, string startHistoryId, string? pageToken, CancellationToken cancellationToken = default)
    {
        string path = "history?maxResults=50&historyTypes=messageAdded&startHistoryId=" + History(startHistoryId)
            + "&fields=historyId,nextPageToken,history(id,messagesAdded(message(id,threadId)))" + Page(pageToken);
        JsonElement json;
        try { json = await SendAsync(call, HttpMethod.Get, path, null, cancellationToken); }
        catch (ConnectorProviderException exception) when (exception.Code == "ProviderItemNotFound") { throw new ConnectorProviderException("MailboxCursorExpired"); }
        List<MailboxChange> changes = [];
        if (json.TryGetProperty("history", out JsonElement history))
            foreach (JsonElement item in history.EnumerateArray())
            {
                string id = History(Text(item, "id", 40));
                if (!item.TryGetProperty("messagesAdded", out JsonElement messages)) continue;
                foreach (JsonElement added in messages.EnumerateArray())
                {
                    JsonElement message = added.GetProperty("message");
                    changes.Add(new(id, Identifier(Text(message, "id", 256)), Identifier(Text(message, "threadId", 256))));
                    if (changes.Count > 200) throw new ConnectorProviderException("MailboxPageLimit");
                }
            }
        return new(History(Text(json, "historyId", 40)), changes, Optional(json, "nextPageToken", 4096));
    }

    public async Task<MailboxMessagePage> ListMessagesAsync(MailboxProviderCall call, IReadOnlyList<string> labelIds, DateTime? sinceUtc, string? pageToken, CancellationToken cancellationToken = default)
    {
        ValidateLabels(labelIds);
        string path = "messages?maxResults=50&fields=messages(id,threadId),nextPageToken" + Page(pageToken);
        foreach (string label in labelIds) path += "&labelIds=" + Uri.EscapeDataString(label);
        if (sinceUtc.HasValue)
        {
            if (sinceUtc.Value.Kind != DateTimeKind.Utc) throw new ArgumentException("Mailbox recovery uses UTC bounds.");
            path += "&q=" + Uri.EscapeDataString("after:" + new DateTimeOffset(sinceUtc.Value).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));
        }
        JsonElement json = await SendAsync(call, HttpMethod.Get, path, null, cancellationToken);
        List<MailboxMessageReference> messages = [];
        if (json.TryGetProperty("messages", out JsonElement array))
            foreach (JsonElement item in array.EnumerateArray())
            {
                messages.Add(new(Identifier(Text(item, "id", 256)), Identifier(Text(item, "threadId", 256)), DateTime.UnixEpoch, []));
                if (messages.Count > 50) throw new ConnectorProviderException("MailboxPageLimit");
            }
        return new(messages, Optional(json, "nextPageToken", 4096));
    }

    public async Task<MailboxMessageReference> GetMessageAsync(MailboxProviderCall call, string messageId, CancellationToken cancellationToken = default)
    {
        JsonElement json = await SendAsync(call, HttpMethod.Get, "messages/" + Uri.EscapeDataString(Identifier(messageId))
            + "?format=metadata&fields=id,threadId,internalDate,labelIds", null, cancellationToken);
        if (Text(json, "id", 256) != messageId || !long.TryParse(Text(json, "internalDate", 20), NumberStyles.None, CultureInfo.InvariantCulture, out long milliseconds)) throw Invalid();
        IReadOnlyList<string> labels = json.TryGetProperty("labelIds", out JsonElement array)
            ? array.EnumerateArray().Select(value => Identifier(value.GetString() ?? "")).ToList() : [];
        if (labels.Count > 100) throw Invalid();
        try { return new(messageId, Identifier(Text(json, "threadId", 256)), DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).UtcDateTime, labels); }
        catch (ArgumentOutOfRangeException) { throw Invalid(); }
    }

    private async Task<JsonElement> SendAsync(MailboxProviderCall call, HttpMethod method, string path, string? body, CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = Request(call, method, path);
        if (body is not null) request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        return await GmailHttp.JsonAsync(clients.CreateClient("Gmail"), request, 262_144, cancellationToken);
    }
    private static HttpRequestMessage Request(MailboxProviderCall call, HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, "https://gmail.googleapis.com/gmail/v1/users/me/" + path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", call.AccessToken);
        return request;
    }
    internal static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    internal static bool Resource(string value, string kind) => value is { Length: <= 512 }
        && Regex.IsMatch(value, "^projects/[a-z][a-z0-9-]{4,62}/" + kind + "/[a-zA-Z][a-zA-Z0-9._~+%-]{2,254}$", RegexOptions.NonBacktracking);
    internal static string History(string value) => value is { Length: > 0 and <= 40 } && value.All(char.IsAsciiDigit) ? value : throw Invalid();
    private static string Identifier(string value) => value is { Length: > 0 and <= 256 }
        && Regex.IsMatch(value, "^[a-zA-Z0-9_-]+$", RegexOptions.NonBacktracking) ? value : throw Invalid();
    private static void ValidateLabels(IReadOnlyList<string> labels)
    { if (labels.Count > 10) throw new ArgumentException("At most ten mailbox labels are supported."); foreach (string label in labels) _ = Identifier(label); }
    private static string Page(string? page) => page is null ? "" : page.Length is > 0 and <= 4096 ? "&pageToken=" + Uri.EscapeDataString(page) : throw Invalid();
    private static string Text(JsonElement element, string key, int limit) => Optional(element, key, limit) ?? throw Invalid();
    private static string? Optional(JsonElement element, string key, int limit) => !element.TryGetProperty(key, out JsonElement value) ? null
        : value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text && text.Length <= limit && !text.Any(char.IsControl) ? text : throw Invalid();
    private static ConnectorProviderException Invalid() => new("ProviderResponseInvalid");
}
