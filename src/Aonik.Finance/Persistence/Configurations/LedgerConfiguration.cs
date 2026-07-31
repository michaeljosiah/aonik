using Aonik.Finance.Entities.Ledger;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Finance.Persistence.Configurations;

/// <summary>
/// Spec 088 §5.1 — the canonical-ledger marker. Previously <see cref="Ledger"/> was mapped purely
/// by convention; this exists to pin the one invariant that matters.
/// </summary>
public class LedgerConfiguration : IEntityTypeConfiguration<Ledger>
{
    public void Configure(EntityTypeBuilder<Ledger> builder)
    {
        // At most one canonical ledger per tenant. A service-level check would lose the race
        // between two concurrent "mark canonical" calls and leave the resolver with two answers
        // and no way to choose — the exact ambiguity ILedgerResolver refuses to guess through.
        // Filtered so the many non-canonical ledgers a tenant may hold are unconstrained.
        builder.HasIndex(x => x.TenantId)
            .IsUnique()
            .HasFilter("[IsCanonical] = 1");
    }
}
