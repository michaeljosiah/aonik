using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Persistence;
using Aonik.Voice.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Voice.Persistence;

/// <summary>
/// Module-scoped DbContext for the Voice module's owned entities. Mirrors the AONIK
/// convention of one DbContext per module for runtime isolation; migrations still ship via
/// the canonical <c>AonikDbContext</c>. Entities are physically in the same SQL database.
///
/// <para>
/// See <c>docs/specifications/024.unified-speech-config-and-composer.md</c> §"Persistence".
/// </para>
/// </summary>
internal class VoiceDbContext : AonikDbContextBase
{
    public DbSet<SpeechProviderEntity> SpeechProviders { get; set; } = null!;
    public DbSet<VoiceRecipeEntity> VoiceRecipes { get; set; } = null!;

    public VoiceDbContext(
        DbContextOptions<VoiceDbContext> options,
        ITenantProvider? tenantProvider = null,
        ICurrentUserProvider? currentUserProvider = null,
        IClock? clock = null)
        : base(options, tenantProvider, currentUserProvider, clock)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema(SchemaNames.Default);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(VoiceDbContext).Assembly);

        ConfigureRowVersions(modelBuilder);
        ApplyTenantQueryFilters(modelBuilder);
    }
}
