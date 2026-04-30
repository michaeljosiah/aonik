using Aonik.Agents.Entities.Workflows;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Agents.Persistence.Configurations.Workflows;

internal class WorkflowConfiguration : IEntityTypeConfiguration<Workflow>
{
    public void Configure(EntityTypeBuilder<Workflow> builder)
    {
        builder.ToTable("Workflows", SchemaNames.Default);

        builder.Property(w => w.Slug).HasMaxLength(100).IsRequired();
        builder.Property(w => w.Name).HasMaxLength(200).IsRequired();
        builder.Property(w => w.Description).HasMaxLength(2000);
        builder.Property(w => w.OwnerColor).HasMaxLength(16);
        builder.Property(w => w.State).HasMaxLength(32).IsRequired();
        builder.Property(w => w.Version).HasMaxLength(32).IsRequired();
        builder.Property(w => w.ContributorsJson).HasColumnType("nvarchar(max)");

        // Slug is unique per tenant — used as the URL key for the editor.
        builder.HasIndex(w => new { w.TenantId, w.Slug })
            .IsUnique()
            .HasDatabaseName("IX_Workflows_TenantId_Slug");

        // Common list-page sort: by tenant + state for the filter pills.
        builder.HasIndex(w => new { w.TenantId, w.State })
            .HasDatabaseName("IX_Workflows_TenantId_State");
    }
}
