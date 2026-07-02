using Aonik.Commerce.Entities.Sourcing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Commerce.Persistence.Configurations.Sourcing;

public class GoodsReceiptConfiguration : IEntityTypeConfiguration<GoodsReceipt>
{
    public void Configure(EntityTypeBuilder<GoodsReceipt> builder)
    {
        builder.HasKey(x => x.Id);

        // Same length as the Order spine's IdempotencyKey column — the two keys travel together
        // in retry stories (Spec 054 §8 mirrors CreateOrderCommand.IdempotencyKey).
        builder.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(16);
        builder.Property(x => x.Notes).HasMaxLength(1024);

        // The §8 resolve-or-create idempotency backstop: one receipt per (tenant, key). The
        // service resolves the key BEFORE any stock/cost mutation; this index is the SQL Server
        // authority under concurrency (InMemory does not enforce unique indexes — the service
        // pre-check is covered by unit tests).
        builder.HasIndex(x => new { x.TenantId, x.IdempotencyKey }).IsUnique();

        // "All receipts for this PO" — the received-vs-ordered cumulative reads (§9).
        builder.HasIndex(x => new { x.TenantId, x.PurchaseOrderId });
    }
}
