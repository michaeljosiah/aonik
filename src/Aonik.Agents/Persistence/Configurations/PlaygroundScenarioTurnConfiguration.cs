using Aonik.Agents.Entities;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Agents.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the <see cref="PlaygroundScenarioTurn"/> entity.
/// </summary>
internal class PlaygroundScenarioTurnConfiguration : IEntityTypeConfiguration<PlaygroundScenarioTurn>
{
    public void Configure(EntityTypeBuilder<PlaygroundScenarioTurn> builder)
    {
        builder.ToTable("PlaygroundScenarioTurns", SchemaNames.Default);

        builder.Property(x => x.Role)
            .HasMaxLength(20)
            .IsRequired();

        builder.HasIndex(x => new { x.PlaygroundScenarioId, x.SortOrder })
            .HasDatabaseName("IX_PlaygroundScenarioTurns_ScenarioId_SortOrder");
    }
}
