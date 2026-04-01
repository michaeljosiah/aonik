using Aonik.Platform.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Platform.Persistence.Configurations;

public class PreRegistrationChallengeConfiguration : IEntityTypeConfiguration<PreRegistrationChallenge>
{
    public void Configure(EntityTypeBuilder<PreRegistrationChallenge> builder)
    {
        builder.ToTable("PreRegistrationChallenges");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.TenantId)
            .IsRequired();

        builder.Property(c => c.Phone)
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(c => c.CodeHash)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(c => c.ExpiresAt)
            .IsRequired();

        builder.Property(c => c.AttemptCount)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(c => c.Status)
            .IsRequired()
            .HasDefaultValue(VerificationStatus.Pending);

        builder.HasIndex(c => new { c.TenantId, c.Phone })
            .HasDatabaseName("IX_PreRegistrationChallenge_Tenant_Phone");
    }
}
