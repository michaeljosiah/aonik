using Aonik.Agents.Entities;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Agents.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the AgentRun entity.
/// Maps to the dbo schema (created by existing migrations).
/// </summary>
internal class AgentRunConfiguration : IEntityTypeConfiguration<AgentRun>
{
    public void Configure(EntityTypeBuilder<AgentRun> builder)
    {
        builder.ToTable("AgentRuns", SchemaNames.Default);
    }
}
