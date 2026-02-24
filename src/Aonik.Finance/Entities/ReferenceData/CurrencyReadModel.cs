using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.ReferenceData;

/// <summary>
/// Read-only projection of the Currency entity for cross-module queries.
/// The authoritative Currency entity lives in Aonik.Platform.
/// TEMPORARY: Will be replaced by service contracts when inter-module
/// communication is fully implemented.
/// </summary>
public class CurrencyReadModel : AuditableEntity
{
    public Guid? TenantId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? NumericCode { get; set; }
    public int? MinorUnit { get; set; }
    public string? WithdrawalDate { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}
