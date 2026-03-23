using Aonik.Ai.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Ai.Persistence.Configurations;

internal sealed class AiModelConfiguration : IEntityTypeConfiguration<AiModel>
{
    public void Configure(EntityTypeBuilder<AiModel> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ExternalModelKey)
            .HasMaxLength(300);

        builder.Property(x => x.CostProfileJson)
            .IsRequired();

        builder.Property(x => x.LatencyProfileJson)
            .IsRequired();

        builder.Property(x => x.PolicyTagsJson)
            .IsRequired();

        builder.HasIndex(x => new { x.AiProviderId, x.ExternalModelKey })
            .IsUnique()
            .HasFilter("[ExternalModelKey] IS NOT NULL AND [IsDeleted] = 0");
    }
}
