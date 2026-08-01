using Aonik.PersonalFinance.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.PersonalFinance.Persistence.Configurations;

internal sealed class HouseholdMemberConfiguration : IEntityTypeConfiguration<HouseholdMember>
{
    public void Configure(EntityTypeBuilder<HouseholdMember> builder)
    {
        builder.HasIndex(item => new { item.TenantId, item.UserId });

        // Spec 086 §10.2 — the one NON-ADDITIVE change in this extraction, and it is unavoidable.
        // The previous index was an unfiltered unique on (TenantId, HouseholdId, UserId). SQL Server
        // treats NULL as a value there and permits only ONE per key, so the moment UserId went
        // nullable a SECOND party-only member in the same group could never insert — which is the
        // single thing this whole spec exists to enable. Filtered, plus party-based uniqueness.
        builder.HasIndex(item => new { item.TenantId, item.HouseholdId, item.UserId })
            .IsUnique()
            .HasFilter("[UserId] IS NOT NULL");

        builder.HasIndex(item => new { item.TenantId, item.HouseholdId, item.PartyId })
            .IsUnique()
            .HasDatabaseName("IX_AnkHouseholdMembers_TenantId_HouseholdId_PartyId")
            .HasFilter("[PartyId] IS NOT NULL");

        builder.Property(item => item.Role)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(item => item.InvitationStatus)
            .IsRequired()
            .HasMaxLength(32)
            // Spec 086: taken from the SharedKernel contract rather than PersonalFinance's copy.
            // Groups cannot reference PersonalFinance — that is the inversion this module exists to
            // avoid — and the two constants are the same string, so the shared one is the authority
            // and PersonalFinance's becomes the legacy mirror.
            .HasDefaultValue(Aonik.SharedKernel.Abstractions.Groups.GroupMemberStatuses.Accepted);
    }
}
