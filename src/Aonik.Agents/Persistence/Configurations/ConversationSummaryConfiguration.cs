using Aonik.Agents.Entities;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Agents.Persistence.Configurations;

internal class ConversationSummaryConfiguration : IEntityTypeConfiguration<ConversationSummary>
{
    public void Configure(EntityTypeBuilder<ConversationSummary> builder)
    {
        builder.ToTable("ConversationSummaries", SchemaNames.Default);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId)
            .IsRequired();

        builder.Property(x => x.UserId)
            .IsRequired();

        builder.Property(x => x.ChatThreadId)
            .IsRequired();

        builder.Property(x => x.SessionStartedAt)
            .IsRequired();

        builder.Property(x => x.SummaryText)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(x => x.KeyDecisionsJson)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.OpenLoopsJson)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.RecommendationOutcomesJson)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        // FK to ChatThread with cascade delete
        builder.HasOne(x => x.ChatThread)
            .WithMany()
            .HasForeignKey(x => x.ChatThreadId)
            .OnDelete(DeleteBehavior.Cascade);

        // Primary query: recent summaries for a user
        builder.HasIndex(x => new { x.TenantId, x.UserId, x.SessionStartedAt })
            .IsDescending(false, false, true)
            .HasDatabaseName("IX_ConversationSummaries_TenantUser_SessionStart");

        // Idempotency: one summary per chat thread
        builder.HasIndex(x => x.ChatThreadId)
            .IsUnique()
            .HasDatabaseName("IX_ConversationSummaries_ChatThreadId");
    }
}
