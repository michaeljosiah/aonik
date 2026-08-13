using Aonik.Ai.Entities.Safety;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Ai.Persistence.Configurations;

public class SafetyDecisionConfiguration : IEntityTypeConfiguration<SafetyDecision>
{
    public void Configure(EntityTypeBuilder<SafetyDecision> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SafetyBand).IsRequired().HasMaxLength(32);
        builder.Property(x => x.Modality).IsRequired().HasMaxLength(16);
        builder.Property(x => x.Layer).IsRequired().HasMaxLength(8);
        builder.Property(x => x.Outcome).IsRequired().HasMaxLength(32);
        builder.Property(x => x.Categories).HasMaxLength(256);
        builder.Property(x => x.SafetyPolicyVersion).IsRequired().HasMaxLength(32);
        builder.Property(x => x.ClassifierRunIds).HasMaxLength(512);

        builder.HasIndex(x => new { x.TenantId, x.SubjectPartyId });

        // The sweep predicate. Without it, retention degrades into a table scan on every run and
        // quietly stops being run at all.
        builder.HasIndex(x => new { x.TenantId, x.ExpiresAt, x.AnonymisedAt });
    }
}

public class SafetyIncidentConfiguration : IEntityTypeConfiguration<SafetyIncident>
{
    public void Configure(EntityTypeBuilder<SafetyIncident> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Category).IsRequired().HasMaxLength(48);
        builder.Property(x => x.AppealState).IsRequired().HasMaxLength(16);

        builder.HasIndex(x => new { x.TenantId, x.SubjectPartyId });
        builder.HasIndex(x => new { x.TenantId, x.SafetyDecisionId });

        // Legal holds are queried on every sweep and must never be missed by a scan timing out.
        builder.HasIndex(x => new { x.TenantId, x.IsUnderLegalHold });
    }
}

public class SafetyArtefactConfiguration : IEntityTypeConfiguration<SafetyArtefact>
{
    public void Configure(EntityTypeBuilder<SafetyArtefact> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Reference).IsRequired().HasMaxLength(512);

        builder.HasIndex(x => new { x.TenantId, x.SafetyIncidentId });
        builder.HasIndex(x => new { x.TenantId, x.ExpiresAt, x.IsUnderLegalHold });
    }
}

public class SafetyPolicyConfiguration : IEntityTypeConfiguration<SafetyPolicy>
{
    public void Configure(EntityTypeBuilder<SafetyPolicy> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Version).IsRequired().HasMaxLength(32);
        builder.Property(x => x.SafetyBand).IsRequired().HasMaxLength(32);
        builder.Property(x => x.ThresholdsJson).IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.SafetyBand, x.IsActive });
    }
}
