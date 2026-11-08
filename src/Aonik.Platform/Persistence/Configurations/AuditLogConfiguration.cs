using Aonik.Platform.Entities.Compliance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Platform.Persistence.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");

        builder.HasKey(a => a.Id);

        builder.HasIndex(a => new { a.TenantId, a.IsDeleted, a.Timestamp });
        builder.HasIndex(a => new { a.TenantId, a.IsDeleted, a.CorrelationId, a.Timestamp });
        builder.HasIndex(a => new { a.TenantId, a.IsDeleted, a.ResourceId, a.Timestamp });

        builder.Property(a => a.CorrelationId)
            .HasMaxLength(200)
            .HasDefaultValue(string.Empty);
    }
}
