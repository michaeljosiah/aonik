using Aonik.SharedKernel.Primitives;

namespace Aonik.PersonalFinance.Entities;

public class StatementImportRow : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid StatementImportId { get; set; }
    public int RowNumber { get; set; }
    public string? OccurredAtRaw { get; set; }
    public string? AmountRaw { get; set; }
    public string? DescriptionRaw { get; set; }
    public string? MerchantRaw { get; set; }
    public string? CurrencyRaw { get; set; }
    public DateTime? NormalizedOccurredAt { get; set; }
    public decimal? NormalizedAmount { get; set; }
    public string? NormalizedCurrency { get; set; }
    public string? NormalizedDescription { get; set; }
    public string ParseStatus { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public string? Fingerprint { get; set; }
}
