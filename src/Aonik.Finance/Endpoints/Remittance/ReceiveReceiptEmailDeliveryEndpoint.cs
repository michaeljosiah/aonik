using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

using Aonik.Finance.Contracts.Services.Remittance;
using Aonik.Finance.Services.Remittance;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;

namespace Aonik.Finance.Endpoints.Remittance;

public sealed class ReceiveReceiptEmailDeliveryEndpoint(
    IOptionsMonitor<ReceiptEmailDeliveryOptions> options, ITenantContext tenantContext,
    IReceiptEmailDeliveryReceiver receiver, IClock clock) : EndpointWithoutRequest<object>
{
    private const int MaxBytes = 256 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public override void Configure()
    {
        Post("/integrations/email/azure/{tenantId:guid}/events");
        AllowAnonymous(); // Dedicated Event Grid subscription authentication below; never user/tenant headers.
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        HttpContext.Response.Headers.CacheControl = "no-store";
        var tenantId = Route<Guid>("tenantId");
        var subscriptionName = HttpContext.Request.Headers["aeg-subscription-name"].ToString();
        var secret = HttpContext.Request.Headers["X-Aonik-Integration-Key"];
        var subscription = options.CurrentValue.Subscriptions.FirstOrDefault(item => item.TenantId != Guid.Empty && item.TenantId == tenantId &&
            !string.IsNullOrWhiteSpace(item.Topic) && !string.IsNullOrWhiteSpace(item.SenderAddress) && !string.IsNullOrWhiteSpace(item.SubscriptionName) &&
            string.Equals(item.SubscriptionName, subscriptionName, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(item.SharedSecret) && item.SharedSecret.Length is >= 32 and <= 4096 && secret.Count == 1 &&
            CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(item.SharedSecret)), SHA256.HashData(Encoding.UTF8.GetBytes(secret[0] ?? ""))));
        if (subscription is null) { await Send.UnauthorizedAsync(ct); return; }
        if (!HttpContext.Request.HasJsonContentType()) { await Send.StatusCodeAsync(StatusCodes.Status415UnsupportedMediaType, ct); return; }
        if (HttpContext.Request.ContentLength > MaxBytes) { await Send.StatusCodeAsync(StatusCodes.Status413PayloadTooLarge, ct); return; }

        try
        {
            using var body = new MemoryStream();
            var buffer = new byte[8192];
            int read;
            while ((read = await HttpContext.Request.Body.ReadAsync(buffer, ct)) > 0)
            {
                if (body.Length + read > MaxBytes) { await Send.StatusCodeAsync(StatusCodes.Status413PayloadTooLarge, ct); return; }
                await body.WriteAsync(buffer.AsMemory(0, read), ct);
            }
            var events = JsonSerializer.Deserialize<List<DeliveryEvent>>(body.ToArray(), JsonOptions);
            if (events is null || events.Count is < 1 or > 100 || events.Any(item => item is null ||
                !string.Equals(item.Topic?.TrimEnd('/'), subscription.Topic.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("Invalid delivery event batch or source topic.");

            var eventType = HttpContext.Request.Headers["aeg-event-type"].ToString();
            if (eventType == "SubscriptionValidation" && events.Count == 1 && events[0].EventType == "Microsoft.EventGrid.SubscriptionValidationEvent")
            {
                var validationCode = StringValue(events[0].Data, "validationCode");
                if (string.IsNullOrWhiteSpace(validationCode) || validationCode.Length > 128) throw new ArgumentException("Invalid subscription validation event.");
                // Echo only. Never fetch the provider-supplied validationUrl.
                await Send.OkAsync(new { validationResponse = validationCode }, ct);
                return;
            }
            if (eventType != "Notification") throw new ArgumentException("Expected an Event Grid notification.");
            var reports = new List<ReceiptEmailDeliveryReport>();
            foreach (var item in events.Where(item => item.EventType == "Microsoft.Communication.EmailDeliveryReportReceived"))
            {
                if (!string.Equals(StringValue(item.Data, "sender"), subscription.SenderAddress, StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("The delivery sender does not match this subscription.");
                if (string.IsNullOrWhiteSpace(item.Id) || item.Id.Length > 128 || !Guid.TryParse(StringValue(item.Data, "messageId"), out var messageId) || messageId == Guid.Empty ||
                    !ReceiptEmailDelivery.ReportStatuses.Contains(StringValue(item.Data, "status") ?? "") ||
                    !item.Data.TryGetProperty("deliveryAttemptTimeStamp", out var attempted) || attempted.ValueKind != JsonValueKind.String || !attempted.TryGetDateTimeOffset(out var attemptedAt) ||
                    attemptedAt == default || attemptedAt.UtcDateTime > clock.UtcNow.AddMinutes(5))
                    throw new ArgumentException("Invalid email delivery event.");
                var recipient = StringValue(item.Data, "recipient");
                if (string.IsNullOrWhiteSpace(recipient) || recipient.Length > 320) throw new ArgumentException("Invalid email delivery recipient.");
                reports.Add(new ReceiptEmailDeliveryReport(item.Id, messageId, recipient, StringValue(item.Data, "status")!, attemptedAt));
            }
            tenantContext.TenantId = tenantId;
            tenantContext.ResolutionSource = "ReceiptEmailEventGrid";
            foreach (var report in reports) await receiver.RecordAsync(report, ct);
            await Send.OkAsync(new { accepted = true }, ct);
        }
        catch (JsonException) { AddError("Invalid delivery event JSON."); await Send.ErrorsAsync(400, ct); }
        catch (ArgumentException ex) { AddError(ex.Message); await Send.ErrorsAsync(400, ct); }
    }

    private static string? StringValue(JsonElement data, string name) => data.ValueKind == JsonValueKind.Object && data.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private sealed record DeliveryEvent(string? Id, string? Topic, string? EventType, JsonElement Data);
}
