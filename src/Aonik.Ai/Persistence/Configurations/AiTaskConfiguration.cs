using Aonik.Ai.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Ai.Persistence.Configurations;

internal sealed class AiTaskConfiguration : IEntityTypeConfiguration<AiTask>
{
    public void Configure(EntityTypeBuilder<AiTask> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.UseCase)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.DisplayName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Description)
            .HasMaxLength(2000);

        builder.Property(x => x.Category)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.ExecutionMode)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(x => x.PromptName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.PromptVersion)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(x => x.SystemTemplate)
            .IsRequired();

        builder.Property(x => x.UserTemplate)
            .IsRequired();

        builder.Property(x => x.DeveloperTemplate)
            .IsRequired();

        builder.Property(x => x.VariablesSchemaJson)
            .IsRequired();

        builder.Property(x => x.OutputSchemaJson)
            .IsRequired();

        builder.HasIndex(x => new { x.UseCase, x.TenantId })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.HasIndex(x => new { x.PromptName, x.PromptVersion, x.TenantId })
            .HasFilter("[IsDeleted] = 0");
    }
}
