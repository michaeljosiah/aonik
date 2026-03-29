using Aonik.Ai.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Ai.Persistence.Configurations;

internal class UserMemoryEntryConfiguration : IEntityTypeConfiguration<UserMemoryEntry>
{
    public void Configure(EntityTypeBuilder<UserMemoryEntry> builder)
    {
        builder.HasKey(x => x.Id);

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

        // FK to AiRuns (nullable — only set for AI-inferred entries)
        builder.HasOne<AiRun>()
            .WithMany()
            .HasForeignKey(x => x.AiRunId)
            .OnDelete(DeleteBehavior.SetNull);

        // Self-referencing FK for superseded chain
        builder.HasOne<UserMemoryEntry>()
            .WithMany()
            .HasForeignKey(x => x.SupersededById)
            .OnDelete(DeleteBehavior.SetNull);

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
    }
}
