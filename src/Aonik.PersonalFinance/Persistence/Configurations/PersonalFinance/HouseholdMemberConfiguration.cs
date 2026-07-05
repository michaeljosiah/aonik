using Aonik.PersonalFinance.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.PersonalFinance.Persistence.Configurations;

internal sealed class HouseholdMemberConfiguration : IEntityTypeConfiguration<HouseholdMember>
{
    public void Configure(EntityTypeBuilder<HouseholdMember> builder)
    {
        builder.HasIndex(item => new { item.TenantId, item.UserId });
        builder.HasIndex(item => new { item.TenantId, item.HouseholdId, item.UserId })
            .IsUnique();

        builder.Property(item => item.Role)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(item => item.InvitationStatus)
            .IsRequired()
            .HasMaxLength(32)
            .HasDefaultValue(Aonik.PersonalFinance.Contracts.Models.HouseholdInvitationStatuses.Accepted);
    }
}
