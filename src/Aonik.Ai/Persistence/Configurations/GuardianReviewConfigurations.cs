using Aonik.Ai.Entities.Safety;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Ai.Persistence.Configurations;

/// <summary>
/// Spec 096 §8 / §12 persistence.
///
/// <para>
/// These exist for a reason beyond column widths. <c>AonikDbContext</c> applies module configurations
/// <em>before</em> <c>ConfigureRowVersions</c> and maps tables <em>after</em> it, so an entity that
/// reaches the canonical model only through <c>MapAiTable</c> gets a plain <c>varbinary(max)</c>
/// concurrency column while the runtime <c>AiDbContext</c> maps it as a database-generated
/// <c>rowversion</c> — and every insert through the module context then fails on a non-null
/// constraint. A configuration class is what puts the entity in the model early enough for both models
/// to agree.
/// </para>
/// </summary>
public class PendingContentReviewConfiguration : IEntityTypeConfiguration<PendingContentReview>
{
    public void Configure(EntityTypeBuilder<PendingContentReview> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SafetyBand).IsRequired().HasMaxLength(32);
        builder.Property(x => x.Modality).IsRequired().HasMaxLength(16);
        builder.Property(x => x.Reference).IsRequired().HasMaxLength(512);
        builder.Property(x => x.State).IsRequired().HasMaxLength(16);

        // The guardian's queue, and the sweeper's predicate.
        builder.HasIndex(x => new { x.TenantId, x.SubjectPartyId, x.State });
        builder.HasIndex(x => new { x.TenantId, x.State, x.ExpiresAt });
    }
}

public class ChildSafetyPreferenceConfiguration : IEntityTypeConfiguration<ChildSafetyPreference>
{
    public void Configure(EntityTypeBuilder<ChildSafetyPreference> builder)
    {
        builder.HasKey(x => x.Id);

        // One choice per child, enforced by the database. Two authorised guardians updating
        // concurrently both see no row and both insert; a later read then picks an arbitrary one of
        // them, and whether a child's content is held becomes nondeterministic.
        builder.HasIndex(x => new { x.TenantId, x.SubjectPartyId })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
    }
}

public class SafetyEscalationConfiguration : IEntityTypeConfiguration<SafetyEscalation>
{
    public void Configure(EntityTypeBuilder<SafetyEscalation> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Category).IsRequired().HasMaxLength(48);
        builder.Property(x => x.Notes).HasMaxLength(2048);

        // "Nobody has looked at this yet" must stay cheap to ask.
        builder.HasIndex(x => new { x.TenantId, x.AcknowledgedAt });
        builder.HasIndex(x => new { x.TenantId, x.SafetyIncidentId });
    }
}

public class PreservedMaterialAccessConfiguration : IEntityTypeConfiguration<PreservedMaterialAccess>
{
    public void Configure(EntityTypeBuilder<PreservedMaterialAccess> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Purpose).IsRequired().HasMaxLength(1024);
        builder.Property(x => x.DenialReason).HasMaxLength(256);

        builder.HasIndex(x => new { x.TenantId, x.SafetyIncidentId });
        builder.HasIndex(x => new { x.TenantId, x.ActorPartyId });
    }
}
