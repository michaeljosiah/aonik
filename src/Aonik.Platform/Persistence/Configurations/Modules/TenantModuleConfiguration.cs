using Aonik.Platform.Entities.Modules;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Platform.Persistence.Configurations.Modules;

/// <summary>
/// Spec 097 §6. Discovered by <c>ApplyConfigurationsFromAssembly</c> in both <c>AonikDbContext</c> and
/// <c>PlatformDbContext</c>; the <c>Ank</c> table prefix is applied by each context's MapTable call,
/// exactly as for <c>TenantFeature</c>.
/// </summary>
public class TenantModuleConfiguration : IEntityTypeConfiguration<TenantModule>
{
    public void Configure(EntityTypeBuilder<TenantModule> builder)
    {
        builder.ToTable("TenantModules");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId)
            .IsRequired();

        builder.Property(x => x.ModuleId)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(x => x.Source)
            .IsRequired()
            .HasMaxLength(16);

        builder.Property(x => x.Reason)
            .HasMaxLength(2048);

        // One row per (tenant, module). The service upserts; the engine makes a second row impossible.
        builder.HasIndex(x => new { x.TenantId, x.ModuleId })
            .IsUnique();

        // Explicit even though AonikDbContextBase.ConfigureRowVersions covers every AuditableEntity:
        // an entity that reaches a model without IsRowVersion scaffolds as varbinary(max), which SQL
        // Server can never ALTER into rowversion. Declaring it here makes the mapping independent of
        // call order in the contexts.
        builder.Property(x => x.RowVersion)
            .IsRowVersion();
    }
}
