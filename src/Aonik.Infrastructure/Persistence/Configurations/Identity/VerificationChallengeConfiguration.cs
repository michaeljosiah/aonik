using Aonik.Domain.Identity;
using Aonik.Domain.Identity.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Infrastructure.Persistence.Configurations.Identity;

public class VerificationChallengeConfiguration : IEntityTypeConfiguration<VerificationChallenge>
{
    public void Configure(EntityTypeBuilder<VerificationChallenge> builder)
    {
        builder.ToTable("VerificationChallenges");

        builder.HasKey(challenge => challenge.Id);

        builder.Property(challenge => challenge.TenantId)
            .IsRequired();

        builder.Property(challenge => challenge.UserId)
            .IsRequired();

        builder.Property(challenge => challenge.Channel)
            .IsRequired();

        builder.Property(challenge => challenge.Target)
            .IsRequired()
            .HasMaxLength(320);

        builder.Property(challenge => challenge.CodeHash)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(challenge => challenge.ExpiresAt)
            .IsRequired();

        builder.Property(challenge => challenge.AttemptCount)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(challenge => challenge.Status)
            .IsRequired()
            .HasDefaultValue(VerificationStatus.Pending);

        builder.HasIndex(challenge => new { challenge.TenantId, challenge.UserId, challenge.Channel })
            .HasDatabaseName("IX_VerificationChallenge_Tenant_User_Channel");

        builder.HasIndex(challenge => new { challenge.TenantId, challenge.Channel, challenge.Target })
            .HasDatabaseName("IX_VerificationChallenge_Tenant_Channel_Target");
    }
}
