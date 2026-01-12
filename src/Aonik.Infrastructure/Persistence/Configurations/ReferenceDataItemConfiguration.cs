using Aonik.Domain.ReferenceData.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Infrastructure.Persistence.Configurations;

public class ReferenceDataItemConfiguration : IEntityTypeConfiguration<ReferenceDataItem>
{
    public void Configure(EntityTypeBuilder<ReferenceDataItem> builder)
    {
        builder.ToTable("ReferenceData");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Type)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Code)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.DisplayName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.SortOrder)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.Type, x.Code })
            .IsUnique();

        builder.HasIndex(x => new { x.Type, x.Code })
            .IsUnique()
            .HasFilter("[TenantId] IS NULL");

        builder.HasIndex(x => new { x.Type, x.SortOrder });
    }
}
