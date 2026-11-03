using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Aonik.Finance.Entities.Orders;

namespace Aonik.Finance.Services.Remittance;

internal sealed record ReceiptEmailRequestDetails(Guid ContactId, string EmailHash, string ReceiptSha256, string MaskedRecipient, string FileName, bool DeliveryTrackingEnabled = false);
internal sealed record ReceiptEmailDeliveryDetails(Guid RequestId, string Status, string ProviderEventIdHash, DateTime ReceivedAt);

internal static class ReceiptEmailDelivery
{
    // Correlate event types to the request so paged history reads avoid JSON scans and
    // fetching every delivery report for the order. The audit payload still carries its ID.
    public static string EventType(Guid requestId) => $"ReceiptDelivery:{requestId:N}";
    public static readonly IReadOnlySet<string> ReportStatuses = new HashSet<string>(StringComparer.Ordinal)
        { "Delivered", "Bounced", "Suppressed", "Quarantined", "FilteredSpam", "Failed", "Expanded" };

    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static Guid EventId(Guid tenantId, Guid requestId, string eventId) => new(SHA256.HashData(Encoding.UTF8.GetBytes($"receipt-delivery:{tenantId:D}:{requestId:D}:{eventId}"))[..16]);
    public static ReceiptEmailRequestDetails RequestDetails(OrderHistoryEvent request) => JsonSerializer.Deserialize<ReceiptEmailRequestDetails>(request.DetailsJson)!;
    public static ReceiptEmailDeliveryDetails DeliveryDetails(OrderHistoryEvent report) => JsonSerializer.Deserialize<ReceiptEmailDeliveryDetails>(report.DetailsJson)!;

    public static (string Status, DateTime? UpdatedAt, bool HasReport) Project(OrderHistoryEvent request, string sendStatus, IEnumerable<OrderHistoryEvent> reports)
    {
        var latest = reports.OrderByDescending(report => DeliveryDetails(report).Status != "Expanded")
            .ThenByDescending(report => report.EventAt).ThenByDescending(report => Rank(DeliveryDetails(report).Status)).ThenBy(report => report.Id).FirstOrDefault();
        if (latest is not null) return (DeliveryDetails(latest).Status, DateTime.SpecifyKind(latest.EventAt, DateTimeKind.Utc), true);
        return (sendStatus == "Failed" ? "NotSent" : RequestDetails(request).DeliveryTrackingEnabled ? "AwaitingReport" : "NotTracked", null, false);
    }

    // A terminal delivery report takes precedence over expansion; ties resolve deterministically.
    private static int Rank(string status) => status switch { "Bounced" => 7, "Suppressed" => 6, "FilteredSpam" => 5, "Quarantined" => 4, "Failed" => 3, "Delivered" => 2, _ => 1 };
}
