using Aonik.Agents.Entities;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Agents.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the <see cref="PlaygroundScenario"/> entity.
/// </summary>
internal class PlaygroundScenarioConfiguration : IEntityTypeConfiguration<PlaygroundScenario>
{
    public void Configure(EntityTypeBuilder<PlaygroundScenario> builder)
    {
        builder.ToTable("PlaygroundScenarios", SchemaNames.Default);

        builder.Property(x => x.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(2000);

        builder.Property(x => x.AgentName)
            .HasMaxLength(100);

        builder.HasIndex(x => x.TenantId)
            .HasDatabaseName("IX_PlaygroundScenarios_TenantId");

        builder.HasMany(x => x.Turns)
            .WithOne(x => x.PlaygroundScenario)
            .HasForeignKey(x => x.PlaygroundScenarioId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
