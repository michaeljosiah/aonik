using Aonik.Voice.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Voice.Persistence.Configurations;

internal sealed class VoiceRecipeEntityConfiguration : IEntityTypeConfiguration<VoiceRecipeEntity>
{
    public void Configure(EntityTypeBuilder<VoiceRecipeEntity> builder)
    {
        builder.ToTable("AnkVoiceRecipes");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.DisplayName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.Kind).HasConversion<string>().IsRequired().HasMaxLength(20);

        // Chained body fields (nullable columns).
        builder.Property(x => x.ChainedSttProviderId).HasMaxLength(80);
        builder.Property(x => x.ChainedTtsProviderId).HasMaxLength(80);
        builder.Property(x => x.ChainedPinnedAgentId).HasMaxLength(120);
        builder.Property(x => x.ChainedVad).HasMaxLength(20);
        builder.Property(x => x.CompositeProviderId).HasMaxLength(80);
        builder.Property(x => x.CompositePinnedAgentId).HasMaxLength(120);

        builder.Property(x => x.Status).HasConversion<string>().IsRequired().HasMaxLength(20);
        builder.Property(x => x.Version).IsRequired();
        builder.Property(x => x.PreviousVersionsJson).IsRequired().HasColumnType("nvarchar(max)");

        builder.HasIndex(x => new { x.TenantId, x.Kind, x.IsDeleted })
            .HasDatabaseName("IX_AnkVoiceRecipes_Tenant_Kind_IsDeleted");

        // Hot-path index for the SpeechProviderLibraryService.GetUsageAsync() reverse-lookup —
        // "which recipes reference this provider id?". Two columns to cover STT and TTS provider
        // refs; the composite ref uses its own index below.
        builder.HasIndex(x => new { x.TenantId, x.ChainedSttProviderId })
            .HasDatabaseName("IX_AnkVoiceRecipes_Tenant_ChainedSttProviderId");
        builder.HasIndex(x => new { x.TenantId, x.ChainedTtsProviderId })
            .HasDatabaseName("IX_AnkVoiceRecipes_Tenant_ChainedTtsProviderId");
        builder.HasIndex(x => new { x.TenantId, x.CompositeProviderId })
            .HasDatabaseName("IX_AnkVoiceRecipes_Tenant_CompositeProviderId");
    }
}
