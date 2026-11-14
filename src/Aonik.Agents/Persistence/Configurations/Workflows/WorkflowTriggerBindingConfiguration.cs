using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Aonik.Agents.Entities.Workflows;

namespace Aonik.Agents.Persistence.Configurations.Workflows;

internal sealed class WorkflowTriggerBindingConfiguration : IEntityTypeConfiguration<WorkflowTriggerBinding>
{
    public void Configure(EntityTypeBuilder<WorkflowTriggerBinding> builder)
    {
        builder.Property(binding => binding.Name).HasMaxLength(160);
        builder.Property(binding => binding.Status).HasMaxLength(24);
        builder.Property(binding => binding.DraftJson).HasColumnType("nvarchar(max)");
        builder.Property(binding => binding.ActiveJson).HasColumnType("nvarchar(max)");
        builder.Property(binding => binding.DraftHash).HasMaxLength(64);
        builder.Property(binding => binding.ActiveHash).HasMaxLength(64);
        builder.Property(binding => binding.ActiveSource).HasMaxLength(40);
        builder.Property(binding => binding.LastError).HasMaxLength(80);
        builder.HasIndex(binding => new { binding.TenantId, binding.WorkflowId, binding.Status });
        builder.HasIndex(binding => new { binding.TenantId, binding.ActiveSource, binding.Status });
        builder.HasIndex(binding => new { binding.TenantId, binding.ActiveAutomationIdentityId, binding.Status });
    }
}
