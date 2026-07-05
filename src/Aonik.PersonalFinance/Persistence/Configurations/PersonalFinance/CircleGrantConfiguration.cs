using Aonik.PersonalFinance.Entities;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.PersonalFinance.Persistence.Configurations;

internal class CircleGrantConfiguration : IEntityTypeConfiguration<CircleGrant>
{
    public void Configure(EntityTypeBuilder<CircleGrant> builder)
    {
        builder.ToTable("CircleGrants", SchemaNames.Default);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Scope).IsRequired().HasMaxLength(16);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(16);

        builder.HasIndex(x => new { x.TenantId, x.OwnerUserId, x.MemberUserId, x.Status });
        builder.HasIndex(x => new { x.TenantId, x.MemberUserId, x.Status });
    }
}
