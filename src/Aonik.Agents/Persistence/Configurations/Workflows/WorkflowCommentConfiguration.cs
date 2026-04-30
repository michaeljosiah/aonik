using Aonik.Agents.Entities.Workflows;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Agents.Persistence.Configurations.Workflows;

internal class WorkflowCommentConfiguration : IEntityTypeConfiguration<WorkflowComment>
{
    public void Configure(EntityTypeBuilder<WorkflowComment> builder)
    {
        builder.ToTable("WorkflowComments", SchemaNames.Default);

        builder.Property(c => c.Author).HasMaxLength(200);
        builder.Property(c => c.Body).HasMaxLength(2000);

        builder.HasIndex(c => c.WorkflowId)
            .HasDatabaseName("IX_WorkflowComments_WorkflowId");
    }
}
