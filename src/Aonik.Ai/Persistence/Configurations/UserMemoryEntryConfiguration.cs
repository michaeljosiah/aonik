using Aonik.Ai.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Ai.Persistence.Configurations;

internal class UserMemoryEntryConfiguration : IEntityTypeConfiguration<UserMemoryEntry>
{
    public void Configure(EntityTypeBuilder<UserMemoryEntry> builder)
    {
        // Constraint/index names are PINNED to their legacy "UserMemoryEntry" form. A raw-SQL sp_rename
        // renamed only the TABLE to AnkUserMemoryEntries (Spec 041 follow-up); the PK/FK/auto-indexes kept
        // the old names in every live DB. Pinning keeps the model in lock-step with reality, so reconciling
        // the table-name drift emits only a (deliberately no-op) RenameTable and zero constraint churn.
        builder.HasKey(x => x.Id)
            .HasName("PK_UserMemoryEntry");

        builder.Property(x => x.TenantId)
            .IsRequired();

        builder.Property(x => x.UserId)
            .IsRequired();

        builder.Property(x => x.EntryType)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Key)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.ValueJson)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.Confidence)
            .IsRequired()
            .HasPrecision(3, 2);

        builder.Property(x => x.Source)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.LastConfirmedAt)
            .IsRequired();

        // ── Decision-aware learning (Spec 041) — nullable for existing types, populated for Rationale.
        builder.Property(x => x.DecisionType)
            .HasMaxLength(80);

        builder.Property(x => x.ConditionsJson)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.StaleWhen)
            .HasMaxLength(512);

        // FK to AiRuns (nullable — only set for AI-inferred entries)
        builder.HasOne<AiRun>()
            .WithMany()
            .HasForeignKey(x => x.AiRunId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("FK_UserMemoryEntry_AnkAiRuns_AiRunId");

        // Self-referencing FK for superseded chain
        builder.HasOne<UserMemoryEntry>()
            .WithMany()
            .HasForeignKey(x => x.SupersededById)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("FK_UserMemoryEntry_UserMemoryEntry_SupersededById");

        // The FK-column indexes EF creates automatically were also named from the legacy entity — pin
        // them too so the reconciliation stays a pure table rename.
        builder.HasIndex(x => x.AiRunId)
            .HasDatabaseName("IX_UserMemoryEntry_AiRunId");
        builder.HasIndex(x => x.SupersededById)
            .HasDatabaseName("IX_UserMemoryEntry_SupersededById");

        // Primary query: current entries for a user
        builder.HasIndex(x => new { x.TenantId, x.UserId, x.SupersededById })
            .HasFilter("[SupersededById] IS NULL")
            .HasDatabaseName("IX_UserMemoryEntries_TenantUser_Current");

        // Filtered by type
        builder.HasIndex(x => new { x.TenantId, x.UserId, x.EntryType })
            .HasDatabaseName("IX_UserMemoryEntries_TenantUser_EntryType");

        // Key lookup for upsert (find existing entry by key to supersede)
        builder.HasIndex(x => new { x.TenantId, x.UserId, x.Key })
            .HasDatabaseName("IX_UserMemoryEntries_TenantUser_Key");

        // Rationale recall filters by decision type (Spec 041) — index it so the type filter seeks
        // instead of scanning all of a user's rationales.
        builder.HasIndex(x => new { x.TenantId, x.UserId, x.DecisionType })
            .HasDatabaseName("IX_UserMemoryEntries_TenantUser_DecisionType");
    }
}
