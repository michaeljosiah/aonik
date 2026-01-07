using Aonik.Domain.Ai.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Infrastructure.Persistence.Configurations;

public class InsightConfiguration : IEntityTypeConfiguration<Insight>
{
    public void Configure(EntityTypeBuilder<Insight> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.SubjectType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.SubjectId)
            .IsRequired();

        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Summary)
            .IsRequired();

        builder.Property(x => x.CreatedUtc)
            .IsRequired();

        builder.HasIndex(x => new { x.SubjectType, x.SubjectId });
        builder.HasIndex(x => x.CreatedUtc);
    }
}
