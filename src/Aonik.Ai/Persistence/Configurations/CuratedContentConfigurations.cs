using Aonik.Ai.Entities.Safety;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Ai.Persistence.Configurations;

public class CuratedCharacterConfiguration : IEntityTypeConfiguration<CuratedCharacter>
{
    public void Configure(EntityTypeBuilder<CuratedCharacter> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CharacterKey).IsRequired().HasMaxLength(64);
        builder.Property(x => x.DisplayName).IsRequired().HasMaxLength(128);
        builder.Property(x => x.ReferenceImageRef).HasMaxLength(512);
        builder.Property(x => x.MinimumSafetyBand).IsRequired().HasMaxLength(32);

        builder.HasIndex(x => new { x.TenantId, x.CharacterKey }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.IsActive });
    }
}

public class StoryTemplateConfiguration : IEntityTypeConfiguration<StoryTemplate>
{
    public void Configure(EntityTypeBuilder<StoryTemplate> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TemplateKey).IsRequired().HasMaxLength(64);
        builder.Property(x => x.DisplayName).IsRequired().HasMaxLength(128);
        builder.Property(x => x.Frame).IsRequired().HasMaxLength(1024);
        builder.Property(x => x.MinimumSafetyBand).IsRequired().HasMaxLength(32);

        builder.HasIndex(x => new { x.TenantId, x.TemplateKey }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.IsActive });
    }
}
