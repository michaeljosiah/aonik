using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.PersonalFinance;

public class FinancialWebhookEvent : AuditableEntity
{
    public Guid? TenantId { get; set; }
    public Guid? UserId { get; set; }
    public Guid? FinancialConnectionId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string ProviderConnectionReference { get; set; } = string.Empty;
    public string ProviderEventType { get; set; } = string.Empty;
    public string ProviderEventCode { get; set; } = string.Empty;
    public string ProcessingStatus { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public string? Error { get; set; }
    public DateTime ReceivedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
}
