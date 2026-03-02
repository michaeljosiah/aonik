using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.PersonalFinance;

public class CategorisationRule : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public string Pattern { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int Priority { get; set; }
    public bool IsActive { get; set; }
    public string MatchType { get; set; } = string.Empty;
    public bool CaseSensitive { get; set; }
    public decimal? MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }
    public Guid? AppliesToAccountId { get; set; }
    public bool CreatedFromUserCorrection { get; set; }
    public string Scope { get; set; } = string.Empty;
    public string ApprovalStatus { get; set; } = string.Empty;
}
