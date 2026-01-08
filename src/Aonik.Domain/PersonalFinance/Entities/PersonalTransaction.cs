using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.PersonalFinance.Entities;

public class PersonalTransaction : AuditableEntity
{
    public Guid PersonalTransactionId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public string SourceType { get; private set; } = string.Empty;
    public Guid SourceId { get; private set; }
    public DateTime OccurredAt { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public string? Merchant { get; private set; }
    public string? Category { get; private set; }
    public decimal Confidence { get; private set; }
    public string? CategorisedBy { get; private set; }
    public string? Notes { get; private set; }
    public string TagsJson { get; private set; } = string.Empty;

    private PersonalTransaction() { }

    public PersonalTransaction(Guid tenantId, Guid userId, string sourceType, Guid sourceId, DateTime occurredAt, decimal amount, string currency)
    {
        PersonalTransactionId = Id;
        TenantId = tenantId;
        UserId = userId;
        SourceType = sourceType;
        SourceId = sourceId;
        OccurredAt = occurredAt;
        Amount = amount;
        Currency = currency;
        Confidence = 0;
        TagsJson = "[]";
    }

    public void Categorise(string category, decimal confidence, string categorisedBy)
    {
        Category = category;
        Confidence = confidence;
        CategorisedBy = categorisedBy;
    }

    public void UpdateMerchant(string merchant)
    {
        Merchant = merchant;
    }

    public void AddNotes(string notes)
    {
        Notes = notes;
    }

    public void UpdateTags(string tagsJson)
    {
        TagsJson = tagsJson;
    }
}
