using Aonik.Platform.Entities.Autonumbering;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Platform.Persistence.Configurations.Autonumbering;

internal class AutonumberReservationConfiguration : IEntityTypeConfiguration<AutonumberReservation>
{
    public void Configure(EntityTypeBuilder<AutonumberReservation> builder)
    {
        builder.ToTable("AutonumberReservations", SchemaNames.Default);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Reference)
            .IsRequired()
            .HasMaxLength(200);

        builder.HasOne<AutonumberProfile>()
            .WithMany()
            .HasForeignKey(x => x.AutonumberProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.AutonumberProfileId, x.SequenceValue })
            .IsUnique();

        builder.HasIndex(x => x.ExpiresAt);
    }
}
