using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Autonumbering.Entities;

public class AutonumberProfile : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string PrefixTemplate { get; set; } = string.Empty;
    public string SuffixTemplate { get; set; } = string.Empty;
    public AutonumberStrategy Strategy { get; set; }
    public AutonumberResetPolicy ResetPolicy { get; set; }
    public int PaddingLength { get; set; }
    public long MinValue { get; set; }
    public long MaxValue { get; set; }
    public long LastIssuedValue { get; set; }
    public DateTime? LastIssuedAt { get; set; }
    public bool IsActive { get; set; }
}
