using Aonik.Domain.Party.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Infrastructure.Persistence.Configurations;

public class PartyRelationshipConfiguration : IEntityTypeConfiguration<PartyRelationship>
{
    public void Configure(EntityTypeBuilder<PartyRelationship> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.RelationshipTypeCode)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Notes)
            .HasMaxLength(500);

        builder.HasIndex(x => x.FromPartyId);
        builder.HasIndex(x => x.ToPartyId);
        builder.HasIndex(x => x.RelationshipTypeCode);
        builder.HasIndex(x => x.IsActive);
    }
}
