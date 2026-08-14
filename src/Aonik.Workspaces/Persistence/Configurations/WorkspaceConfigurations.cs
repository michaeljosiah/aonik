using Aonik.Workspaces.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Workspaces.Persistence.Configurations;

public class WorkspaceConfiguration : IEntityTypeConfiguration<Workspace>
{
    public void Configure(EntityTypeBuilder<Workspace> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Kind).IsRequired().HasMaxLength(32);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Slug).IsRequired().HasMaxLength(120);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(16);

        // Defaulted in the database as well as the entity, so a row created by any path — including a
        // backfill — starts where a new workspace does. A column that begins at 0 for old rows and 1
        // for new ones makes the first sequence depend on how the row arrived.
        builder.Property(x => x.NextSequence).HasDefaultValue(1L);
        builder.Property(x => x.BillingSubscriberKind).IsRequired().HasMaxLength(32);

        builder.HasIndex(x => new { x.TenantId, x.OwnerPartyId });

        // Filtered so a deleted workspace's slug is reusable, and so soft-deleted rows do not block a
        // name the owner has every right to use again.
        builder.HasIndex(x => new { x.TenantId, x.Slug })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
    }
}

public class WorkspaceRevisionConfiguration : IEntityTypeConfiguration<WorkspaceRevision>
{
    public void Configure(EntityTypeBuilder<WorkspaceRevision> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RequestHash).IsRequired().HasMaxLength(64);
        builder.Property(x => x.State).IsRequired().HasMaxLength(16);
        builder.Property(x => x.Message).HasMaxLength(1000);

        // Idempotency. A retried commit lands here, never on the sequence.
        builder.HasIndex(x => new { x.TenantId, x.WorkspaceId, x.CommitId })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        // Ordering. Separate index, separate concern — conflating the two is what made the divergence
        // flow unreachable in the earlier draft.
        builder.HasIndex(x => new { x.TenantId, x.WorkspaceId, x.Sequence })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.HasIndex(x => new { x.TenantId, x.WorkspaceId, x.State });
    }
}

public class WorkspaceFileConfiguration : IEntityTypeConfiguration<WorkspaceFile>
{
    public void Configure(EntityTypeBuilder<WorkspaceFile> builder)
    {
        builder.HasKey(x => x.Id);

        // 400 rather than the filesystem's 260: a manifest path is a logical path and may legitimately
        // be deeper than what a given client can check out.
        builder.Property(x => x.Path).IsRequired().HasMaxLength(400);
        builder.Property(x => x.ContentHash).IsRequired().HasMaxLength(64);
        builder.Property(x => x.ContentType).HasMaxLength(128);

        // One row per path per revision. Also the read path for a manifest.
        builder.HasIndex(x => new { x.TenantId, x.RevisionId, x.Path })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        // "Which revisions still name this blob" — the reference-counting question.
        builder.HasIndex(x => new { x.TenantId, x.ContentHash });
    }
}

public class WorkspaceBlobConfiguration : IEntityTypeConfiguration<WorkspaceBlob>
{
    public void Configure(EntityTypeBuilder<WorkspaceBlob> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ContentHash).IsRequired().HasMaxLength(64);
        builder.Property(x => x.StorageKey).IsRequired().HasMaxLength(512);

        // THE dedupe. Identical bytes stored twice must occupy one blob, and this index is the only
        // thing that actually enforces it — which is why acceptance criterion 3 insists it be proven on
        // LocalDB, where a filtered unique index is real. InMemory would pass a broken implementation.
        builder.HasIndex(x => new { x.TenantId, x.ContentHash })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        // The sweeper's predicate: unreferenced, and not already being deleted by another pass.
        builder.HasIndex(x => new { x.TenantId, x.RefCount, x.IsDeleting });
    }
}

public class BlobPossessionConfiguration : IEntityTypeConfiguration<BlobPossession>
{
    public void Configure(EntityTypeBuilder<BlobPossession> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SubscriberKind).IsRequired().HasMaxLength(32);
        builder.Property(x => x.ContentHash).IsRequired().HasMaxLength(64);

        // One possession row per subscriber per hash. Two rows would each hold their own count and the
        // ceiling claim would be released by whichever reached zero first, while the other still
        // referenced the bytes.
        builder.HasIndex(x => new { x.TenantId, x.SubscriberKind, x.SubscriberId, x.ContentHash })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
    }
}

public class BlobUploadSessionConfiguration : IEntityTypeConfiguration<BlobUploadSession>
{
    public void Configure(EntityTypeBuilder<BlobUploadSession> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SubscriberKind).IsRequired().HasMaxLength(32);
        builder.Property(x => x.DeclaredHash).IsRequired().HasMaxLength(64);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(16);

        builder.HasMany(x => x.Parts)
            .WithOne()
            .HasForeignKey(p => p.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        // The resume lookup, and the sweep predicate.
        builder.HasIndex(x => new { x.TenantId, x.SubscriberKind, x.SubscriberId, x.DeclaredHash, x.Status });
        builder.HasIndex(x => new { x.TenantId, x.Status, x.ExpiresAt });
    }
}

public class BlobUploadPartConfiguration : IEntityTypeConfiguration<BlobUploadPart>
{
    public void Configure(EntityTypeBuilder<BlobUploadPart> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.StorageKey).IsRequired().HasMaxLength(512);

        // One row per part per session. Two rows for one part number would append the same bytes twice
        // into the assembly, and the hash check would then reject a blob the client sent correctly.
        builder.HasIndex(x => new { x.TenantId, x.SessionId, x.PartNumber })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
    }
}
