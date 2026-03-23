using Aonik.Ai.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Ai.Persistence.Configurations;

internal sealed class AiProviderConfiguration : IEntityTypeConfiguration<AiProvider>
{
    public void Configure(EntityTypeBuilder<AiProvider> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ExternalModelProviderKey)
            .HasMaxLength(200);

        builder.Property(x => x.CapabilitiesJson)
            .IsRequired();

        builder.HasIndex(x => x.ExternalModelProviderKey)
            .IsUnique()
            .HasFilter("[ExternalModelProviderKey] IS NOT NULL AND [IsDeleted] = 0");
    }
}
