using Aonik.Finance.Entities.Pricing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Finance.Persistence.Configurations;

public class FxSpreadPolicyConfiguration : IEntityTypeConfiguration<FxSpreadPolicy>
{
    public void Configure(EntityTypeBuilder<FxSpreadPolicy> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.BaseCurrency)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(x => x.TargetCurrency)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(x => x.CustomerTier)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.MarkupBps)
            .HasPrecision(19, 4);

        builder.Property(x => x.MinSpreadPercent)
            .HasPrecision(19, 4);

        builder.Property(x => x.MaxSpreadPercent)
            .HasPrecision(19, 4);

        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => new { x.TenantId, x.BaseCurrency, x.TargetCurrency, x.CustomerTier });
        builder.HasIndex(x => x.Status);
    }
}
