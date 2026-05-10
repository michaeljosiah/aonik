using Aonik.Voice.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Voice.Persistence.Configurations;

/// <summary>
/// EF model for <see cref="VoiceModeSettingsEntity"/>. The tenant id is the primary key
/// (one row per tenant). Tenant scoping query filter on <c>AonikDbContextBase</c> still
/// applies — we just happen to have at most one row per scope.
///
/// <para>
/// We don't index this table explicitly: the PK is the tenant id and queries are always
/// "fetch the single row for the current tenant", so the clustered index is sufficient.
/// </para>
/// </summary>
internal sealed class VoiceModeSettingsEntityConfiguration : IEntityTypeConfiguration<VoiceModeSettingsEntity>
{
    public void Configure(EntityTypeBuilder<VoiceModeSettingsEntity> builder)
    {
        builder.ToTable("AnkVoiceModeSettings");

        // Tenant id IS the primary key — singleton per tenant, no separate Id column needed.
        // The base AuditableEntity still has its `Id` field but we don't use it here; we
        // simply ignore it on this entity.
        builder.HasKey(x => x.TenantId);

        builder.Ignore(x => x.Id);

        builder.Property(x => x.TenantId)
            .ValueGeneratedNever();

        builder.Property(x => x.ActiveRecipeId)
            .HasMaxLength(100);

        builder.Property(x => x.Enabled)
            .IsRequired();
    }
}
