using Aonik.Finance.Entities.PersonalFinance;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Finance.Persistence.Configurations.PersonalFinance;

internal class CircleInviteConfiguration : IEntityTypeConfiguration<CircleInvite>
{
    public void Configure(EntityTypeBuilder<CircleInvite> builder)
    {
        builder.ToTable("CircleInvites", SchemaNames.Default);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Token).IsRequired().HasMaxLength(128);
        builder.Property(x => x.Scope).IsRequired().HasMaxLength(16);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(16);
        builder.Property(x => x.Channel).HasMaxLength(16);

        builder.HasIndex(x => new { x.TenantId, x.Token }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.OwnerUserId, x.Status });
    }
}
