using Aonik.Finance.Entities.PersonalFinance;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Finance.Persistence.Configurations.PersonalFinance;

internal class CareEntityConfiguration : IEntityTypeConfiguration<CareEntity>
{
    public void Configure(EntityTypeBuilder<CareEntity> builder)
    {
        builder.ToTable("CareEntities", SchemaNames.Default);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Kind)
            .IsRequired()
            .HasMaxLength(16);

        builder.Property(x => x.AssetType)
            .HasMaxLength(32);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(120);

        builder.Property(x => x.CountryCode)
            .IsRequired()
            .HasMaxLength(2);

        builder.Property(x => x.Relationship)
            .HasMaxLength(80);

        builder.Property(x => x.Emoji)
            .HasMaxLength(16);

        // AttributesJson is an open extensibility bag → nvarchar(max); no cap.

        // Drives the People &amp; Places grid query (owner + archived state).
        builder.HasIndex(x => new { x.TenantId, x.UserId, x.Archived });
    }
}
