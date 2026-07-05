using Aonik.PersonalFinance.Entities;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.PersonalFinance.Persistence.Configurations;

internal class GoalConfiguration : IEntityTypeConfiguration<Goal>
{
    public void Configure(EntityTypeBuilder<Goal> builder)
    {
        builder.ToTable("Goals", SchemaNames.Default);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.TargetAmount)
            .HasPrecision(19, 4);

        builder.Property(x => x.ProgressAmount)
            .HasPrecision(19, 4);

        builder.Property(x => x.Currency)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(50);

        // ── Compass programme fields (Spec 021) — all nullable ──
        builder.Property(x => x.GoalType)
            .HasMaxLength(50);

        builder.Property(x => x.Strategy)
            .HasMaxLength(2000);

        builder.Property(x => x.RiskAppetite)
            .HasMaxLength(50);

        builder.HasIndex(x => new { x.TenantId, x.UserId });
        builder.HasIndex(x => new { x.TenantId, x.UserId, x.Status });
    }
}
