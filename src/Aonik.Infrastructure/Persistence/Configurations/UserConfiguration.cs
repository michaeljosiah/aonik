using Aonik.Domain.Identity.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        
        builder.HasKey(u => u.Id);
        
        builder.Property(u => u.UserId)
            .IsRequired();
        
        builder.Property(u => u.TenantId)
            .IsRequired();
        
        builder.Property(u => u.ExternalIssuer)
            .IsRequired()
            .HasMaxLength(500);
        
        builder.Property(u => u.ExternalSubject)
            .IsRequired()
            .HasMaxLength(500);
        
        builder.Property(u => u.ExternalTenantId)
            .HasMaxLength(200);
        
        builder.Property(u => u.Email)
            .HasMaxLength(320); // RFC 5321 max length
        
        builder.Property(u => u.Phone)
            .HasMaxLength(50);
        
        builder.Property(u => u.Status)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue("Active");
        
        builder.Property(u => u.PreferencesJson)
            .HasColumnType("nvarchar(max)")
            .HasDefaultValue("{}");
        
        // CRITICAL: Unique index on external identity within tenant
        builder.HasIndex(u => new { u.TenantId, u.ExternalIssuer, u.ExternalSubject })
            .IsUnique()
            .HasDatabaseName("IX_User_TenantId_ExternalIdentity");
        
        // Relationships
        builder.HasMany(u => u.UserRoles)
            .WithOne(ur => ur.User)
            .HasForeignKey(ur => ur.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
