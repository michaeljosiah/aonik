using Aonik.Agents.Entities;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Agents.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the Agent entity.
/// Maps to the dbo schema (created by existing migrations).
/// </summary>
internal class AgentConfiguration : IEntityTypeConfiguration<Agent>
{
    public void Configure(EntityTypeBuilder<Agent> builder)
    {
        builder.ToTable("Agents", SchemaNames.Default);
    }
}
