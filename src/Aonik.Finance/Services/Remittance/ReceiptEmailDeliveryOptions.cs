namespace Aonik.Finance.Services.Remittance;

public sealed class ReceiptEmailDeliveryOptions
{
    public List<ReceiptEmailDeliverySubscription> Subscriptions { get; set; } = [];
}

public sealed class ReceiptEmailDeliverySubscription
{
    public Guid TenantId { get; set; }
    public string Topic { get; set; } = string.Empty;
    public string SubscriptionName { get; set; } = string.Empty;
    public string SenderAddress { get; set; } = string.Empty;
    public string SharedSecret { get; set; } = string.Empty;
}
