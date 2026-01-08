using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Pricing.Entities;

public class FxQuote : AuditableEntity
{
    public Guid FxQuoteId { get; private set; }
    public Guid TenantId { get; private set; }
    public string BaseCurrency { get; private set; } = string.Empty;
    public string TargetCurrency { get; private set; } = string.Empty;
    public decimal Rate { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public string? Provider { get; private set; }
    public string MetadataJson { get; private set; } = string.Empty;

    private FxQuote() { }

    public FxQuote(Guid tenantId, string baseCurrency, string targetCurrency, decimal rate, DateTime expiresAt, string? provider = null)
    {
        FxQuoteId = Id;
        TenantId = tenantId;
        BaseCurrency = baseCurrency;
        TargetCurrency = targetCurrency;
        Rate = rate;
        ExpiresAt = expiresAt;
        Provider = provider;
        MetadataJson = "{}";
    }

    public bool IsExpired()
    {
        return DateTime.UtcNow >= ExpiresAt;
    }

    public void UpdateMetadata(string metadataJson)
    {
        MetadataJson = metadataJson;
    }
}
