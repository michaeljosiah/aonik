using Aonik.Platform.Entities.Party;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Platform.Persistence.Configurations;

public class NotificationPreferenceConfiguration : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PartyId).IsRequired();
        builder.Property(x => x.Email).IsRequired().HasMaxLength(320);

        builder.HasIndex(x => x.PartyId)
            .IsUnique()
            .HasDatabaseName("IX_NotificationPreference_PartyId");

        builder.HasOne(x => x.Party)
            .WithOne()
            .HasForeignKey<NotificationPreference>(x => x.PartyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
