using Aonik.Agents.Entities;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Agents.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the OrchestratorPolicy entity.
/// Maps to the dbo schema (created by existing migrations).
/// </summary>
internal class OrchestratorPolicyConfiguration : IEntityTypeConfiguration<OrchestratorPolicy>
{
    public void Configure(EntityTypeBuilder<OrchestratorPolicy> builder)
    {
        builder.ToTable("OrchestratorPolicies", SchemaNames.Default);
    }
}
