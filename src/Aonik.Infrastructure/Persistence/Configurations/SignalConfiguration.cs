using Aonik.Domain.Ai.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Infrastructure.Persistence.Configurations;

public class SignalConfiguration : IEntityTypeConfiguration<Signal>
{
    public void Configure(EntityTypeBuilder<Signal> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Type)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Severity)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(x => x.Message)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(x => x.CreatedUtc)
            .IsRequired();

        builder.HasIndex(x => x.Type);
        builder.HasIndex(x => x.Severity);
        builder.HasIndex(x => x.CreatedUtc);
    }
}
