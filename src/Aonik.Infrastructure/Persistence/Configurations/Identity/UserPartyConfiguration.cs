using Aonik.Domain.Identity.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Infrastructure.Persistence.Configurations.Identity;

public class UserPartyConfiguration : IEntityTypeConfiguration<UserParty>
{
    public void Configure(EntityTypeBuilder<UserParty> builder)
    {
        builder.ToTable("UserParties");

        builder.HasKey(link => link.Id);

        builder.Property(link => link.TenantId)
            .IsRequired();

        builder.Property(link => link.UserId)
            .IsRequired();

        builder.Property(link => link.PartyId)
            .IsRequired();

        builder.Property(link => link.LinkType)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(link => new { link.TenantId, link.UserId, link.PartyId, link.LinkType })
            .IsUnique()
            .HasDatabaseName("IX_UserParty_Tenant_User_Party_LinkType");

        builder.HasIndex(link => new { link.TenantId, link.UserId })
            .HasDatabaseName("IX_UserParty_Tenant_UserId");
    }
}
