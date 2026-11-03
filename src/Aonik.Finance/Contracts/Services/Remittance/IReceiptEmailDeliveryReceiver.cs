namespace Aonik.Finance.Contracts.Services.Remittance;

/// <summary>Called only after authenticating and resolving the provider subscription's tenant.</summary>
public interface IReceiptEmailDeliveryReceiver
{
    Task RecordAsync(ReceiptEmailDeliveryReport report, CancellationToken cancellationToken = default);
}

public record ReceiptEmailDeliveryReport(string EventId, Guid MessageId, string Recipient, string Status, DateTimeOffset AttemptedAt);
