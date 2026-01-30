using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Aonik.Domain.Autonumbering.Entities;

namespace Aonik.Infrastructure.Persistence.Configurations;

public class AutonumberReservationConfiguration : IEntityTypeConfiguration<AutonumberReservation>
{
    public void Configure(EntityTypeBuilder<AutonumberReservation> builder)
    {
        builder.HasKey(reservation => reservation.Id);

        builder.Property(reservation => reservation.Reference)
            .IsRequired()
            .HasMaxLength(200);

        builder.HasOne<AutonumberProfile>()
            .WithMany()
            .HasForeignKey(reservation => reservation.AutonumberProfileId);

        builder.HasIndex(reservation => new { reservation.AutonumberProfileId, reservation.SequenceValue })
            .IsUnique();

        builder.HasIndex(reservation => reservation.ExpiresAt);
    }
}
