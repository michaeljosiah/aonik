using Aonik.Voice.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Voice.Persistence.Configurations;

/// <summary>
/// EF model for <see cref="SpeechProviderEntity"/>. Mapped to <c>AnkSpeechProviders</c> in
/// <c>dbo</c> following AONIK convention. Tenant scoping + soft-delete query filters are
/// applied automatically by <c>AonikDbContextBase</c>; this config only handles per-column
/// settings + indexes.
/// </summary>
internal sealed class SpeechProviderEntityConfiguration : IEntityTypeConfiguration<SpeechProviderEntity>
{
    public void Configure(EntityTypeBuilder<SpeechProviderEntity> builder)
    {
        builder.ToTable("AnkSpeechProviders");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId)
            .IsRequired();

        builder.Property(x => x.DisplayName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Type)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(x => x.Vendor)
            .IsRequired()
            .HasMaxLength(40);

        builder.Property(x => x.ConfigJson)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(x => x.Version)
            .IsRequired();

        builder.Property(x => x.PreviousVersionsJson)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        // Hot-path index for the admin Providers tab (filter by type, exclude soft-deleted).
        builder.HasIndex(x => new { x.TenantId, x.Type, x.IsDeleted })
            .HasDatabaseName("IX_AnkSpeechProviders_Tenant_Type_IsDeleted");

        // Used by the active-recipe resolver to enforce that a recipe's referenced
        // provider id still exists + is active for this tenant.
        builder.HasIndex(x => new { x.TenantId, x.Status })
            .HasDatabaseName("IX_AnkSpeechProviders_Tenant_Status");
    }
}
