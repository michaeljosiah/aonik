using Aonik.Agents.Entities;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Agents.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the Proposal entity.
/// Maps to the dbo schema (created by existing migrations).
/// </summary>
internal class ProposalConfiguration : IEntityTypeConfiguration<Proposal>
{
    public void Configure(EntityTypeBuilder<Proposal> builder)
    {
        builder.ToTable("Proposals", SchemaNames.Default);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(50);

        // Confidence is a 0..1 score; (5,4) gives 0.0000–9.9999 with headroom.
        builder.Property(x => x.Confidence)
            .HasPrecision(5, 4)
            .HasDefaultValue(0.85m);
    }
}
