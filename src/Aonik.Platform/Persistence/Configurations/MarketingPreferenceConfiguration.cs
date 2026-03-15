using Aonik.Platform.Entities.Party;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Platform.Persistence.Configurations;

public class MarketingPreferenceConfiguration : IEntityTypeConfiguration<MarketingPreference>
{
    public void Configure(EntityTypeBuilder<MarketingPreference> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PartyId).IsRequired();
        builder.Property(x => x.Email).IsRequired().HasMaxLength(320);

        builder.HasIndex(x => x.PartyId)
            .IsUnique()
            .HasDatabaseName("IX_MarketingPreference_PartyId");

        builder.HasOne(x => x.Party)
            .WithOne()
            .HasForeignKey<MarketingPreference>(x => x.PartyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
