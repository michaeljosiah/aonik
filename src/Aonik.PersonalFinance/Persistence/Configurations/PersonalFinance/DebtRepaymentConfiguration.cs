using Aonik.Finance.Entities.PersonalFinance;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Finance.Persistence.Configurations.PersonalFinance;

internal class DebtRepaymentConfiguration : IEntityTypeConfiguration<DebtRepayment>
{
    public void Configure(EntityTypeBuilder<DebtRepayment> builder)
    {
        builder.ToTable("DebtRepayments", SchemaNames.Default);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.CreditorName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.DebtType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.ExpectedAmount)
            .HasPrecision(19, 4);

        builder.Property(x => x.Currency)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(x => x.Frequency)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue("Monthly");

        builder.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue("Active");

        // ── Commitment fields ────────────────────────────────

        builder.Property(x => x.VerificationStatus)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue("Confirmed");

        builder.Property(x => x.Origin)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue("Manual");

        builder.Property(x => x.ConfidenceScore)
            .HasPrecision(5, 4);

        builder.Property(x => x.Notes)
            .HasMaxLength(1000);

        builder.Property(x => x.AccountReference)
            .HasMaxLength(200);

        builder.Property(x => x.LastPaidAmount)
            .HasPrecision(19, 4);

        // ── Indexes ──────────────────────────────────────────

        builder.HasIndex(x => new { x.TenantId, x.UserId });
        builder.HasIndex(x => new { x.TenantId, x.UserId, x.NextDueDate });
        builder.HasIndex(x => new { x.TenantId, x.UserId, x.Status });
        builder.HasIndex(x => new { x.TenantId, x.UserId, x.VerificationStatus });
        builder.HasIndex(x => new { x.TenantId, x.UserId, x.SourceTransactionId })
            .HasFilter("[SourceTransactionId] IS NOT NULL");
    }
}
