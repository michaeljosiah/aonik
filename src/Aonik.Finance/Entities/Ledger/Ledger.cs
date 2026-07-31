using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.Ledger;

public class Ledger : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string BaseCurrency { get; set; } = string.Empty;

    /// <summary>
    /// Marks the ledger a cross-module caller posts to when it has no opinion (Spec 088 §5.1).
    /// At most one per tenant, enforced by a filtered unique index.
    ///
    /// A tenant with several ledgers and none marked is <b>ambiguous, not defaultable</b>: account
    /// codes are unique per ledger and each ledger carries its own base currency, so guessing puts
    /// material entries somewhere unpredictable. <c>ILedgerResolver</c> throws in that case rather
    /// than choosing.
    /// </summary>
    public bool IsCanonical { get; set; }

    public List<LedgerAccount> Accounts { get; set; } = new();
}
