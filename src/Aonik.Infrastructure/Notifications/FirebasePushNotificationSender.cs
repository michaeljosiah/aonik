using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Aonik.Infrastructure.Communication.Configuration;
using Aonik.Platform.Contracts.Models.Notifications;
using Aonik.Platform.Contracts.Services.Notifications;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aonik.Infrastructure.Notifications;

internal sealed class FirebasePushNotificationSender : IPushNotificationSender
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly FcmOptions _options;
    private readonly ILogger<FirebasePushNotificationSender> _logger;

    public FirebasePushNotificationSender(
        HttpClient httpClient,
        IOptions<FcmOptions> options,
        ILogger<FirebasePushNotificationSender> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<PushNotificationDispatchResult> SendAsync(
        PushNotificationDispatchRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_options.IsConfigured || request.Targets.Count == 0)
        {
            return new PushNotificationDispatchResult(Array.Empty<Guid>());
        }

        var accessToken = await GetAccessTokenAsync(cancellationToken);
        var invalidDeviceIds = new List<Guid>();

        foreach (var target in request.Targets.Where(x =>
                     string.Equals(x.Provider, "fcm", StringComparison.OrdinalIgnoreCase)
                     && string.Equals(x.Platform, "android", StringComparison.OrdinalIgnoreCase)))
        {
            using var httpRequest = new HttpRequestMessage(
                HttpMethod.Post,
                $"https://fcm.googleapis.com/v1/projects/{_options.ProjectId}/messages:send");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var payload = new
            {
                message = new
                {
                    token = target.DeviceToken,
                    notification = new
                    {
                        title = request.Title,
                        body = request.Body,
                    },
                    data = BuildDataPayload(request),
                    android = new
                    {
                        priority = "high"
                    }
                }
            };

            httpRequest.Content = new StringContent(
                JsonSerializer.Serialize(payload, JsonOptions),
                Encoding.UTF8,
                "application/json");

            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                continue;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (IsInvalidTokenResponse(body))
            {
                invalidDeviceIds.Add(target.NotificationDeviceId);
                _logger.LogInformation(
                    "Invalidated FCM token for notification device {NotificationDeviceId}.",
                    target.NotificationDeviceId);
                continue;
            }

            _logger.LogWarning(
                "Failed to send FCM push for tenant {TenantId} user {UserId}. Status {StatusCode}. Response: {ResponseBody}",
                request.TenantId,
                request.UserId,
                (int)response.StatusCode,
                body);
        }

        return new PushNotificationDispatchResult(invalidDeviceIds);
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        var credential = GoogleCredential
            .FromJson(_options.ServiceAccountJson)
            .CreateScoped("https://www.googleapis.com/auth/firebase.messaging");

        return await credential.UnderlyingCredential.GetAccessTokenForRequestAsync(cancellationToken: cancellationToken);
    }

    private static Dictionary<string, string> BuildDataPayload(PushNotificationDispatchRequest request)
    {
        var payload = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["type"] = request.Type,
            ["source"] = request.Source,
            ["severity"] = request.Severity,
        };

        if (!string.IsNullOrWhiteSpace(request.ActionUrl))
        {
            payload["actionUrl"] = request.ActionUrl.Trim();
        }

        return payload;
    }

    private static bool IsInvalidTokenResponse(string body)
    {
        return body.Contains("UNREGISTERED", StringComparison.OrdinalIgnoreCase)
            || body.Contains("registration-token-not-registered", StringComparison.OrdinalIgnoreCase)
            || body.Contains("Invalid registration token", StringComparison.OrdinalIgnoreCase);
    }
}
