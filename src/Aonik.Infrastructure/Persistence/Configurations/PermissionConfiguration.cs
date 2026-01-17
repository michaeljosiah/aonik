using Aonik.Domain.Identity.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Infrastructure.Persistence.Configurations;

public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("Permissions");
        
        builder.HasKey(p => p.Id);
        
        builder.Property(p => p.Key)
            .IsRequired()
            .HasMaxLength(200);
        
        builder.Property(p => p.Description)
            .HasMaxLength(500);
        
        // Unique index on permission key (global permissions, not tenant-scoped)
        builder.HasIndex(p => p.Key)
            .IsUnique()
            .HasDatabaseName("IX_Permission_Key");
    }
}
