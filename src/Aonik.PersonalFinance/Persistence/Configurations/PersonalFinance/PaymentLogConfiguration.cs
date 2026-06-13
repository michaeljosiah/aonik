using Aonik.Finance.Entities.PersonalFinance;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Finance.Persistence.Configurations.PersonalFinance;

internal class PaymentLogConfiguration : IEntityTypeConfiguration<PaymentLog>
{
    public void Configure(EntityTypeBuilder<PaymentLog> builder)
    {
        builder.ToTable("PaymentLogs", SchemaNames.Default);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Currency)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(x => x.Channel)
            .IsRequired()
            .HasMaxLength(16);

        builder.Property(x => x.Origin)
            .IsRequired()
            .HasMaxLength(24);

        builder.Property(x => x.CorroborationStatus)
            .IsRequired()
            .HasMaxLength(16);

        builder.Property(x => x.Note)
            .HasMaxLength(2000);

        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.ApproxGbp).HasPrecision(18, 2);

        // Profile history + entity totals (Spec 043 §8/§9).
        builder.HasIndex(x => new { x.TenantId, x.UserId, x.CareEntityId, x.Date });
        // Cycle/commitment lookups (Spec 044 mark-done).
        builder.HasIndex(x => new { x.TenantId, x.UserId, x.CommitmentId });
        // Offline replay-safe create — one log per idempotency key.
        builder.HasIndex(x => new { x.TenantId, x.UserId, x.IdempotencyKey })
            .IsUnique()
            .HasFilter("[IdempotencyKey] IS NOT NULL");
        // Corroboration dedup — one log per bank transaction.
        builder.HasIndex(x => x.SourceTransactionId)
            .IsUnique()
            .HasFilter("[SourceTransactionId] IS NOT NULL");
    }
}
