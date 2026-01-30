using Aonik.Domain.Pricing.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Infrastructure.Persistence.Configurations;

public class FxRefreshScheduleConfiguration : IEntityTypeConfiguration<FxRefreshSchedule>
{
    public void Configure(EntityTypeBuilder<FxRefreshSchedule> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.CronExpression)
            .IsRequired()
            .HasMaxLength(120);

        builder.Property(x => x.TimeZone)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.MetadataJson)
            .HasColumnType("nvarchar(max)");

        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
        builder.HasIndex(x => x.IsEnabled);
    }
}
