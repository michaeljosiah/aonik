using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.SharedKernel.Events.Outbox;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable($"{ModuleTablePrefixes.Default}OutboxMessages", SchemaNames.Default);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.EventId).IsRequired();
        builder.Property(x => x.EventType).IsRequired().HasMaxLength(512);
        builder.Property(x => x.Payload).IsRequired();
        builder.Property(x => x.TraceParent).HasMaxLength(128);
        builder.Property(x => x.Error).HasMaxLength(4000);
        builder.Property(x => x.ClaimedBy).HasMaxLength(128);

        // Hot path (M8): the processor drains unprocessed, non-dead-lettered, due rows
        // oldest-first — `ProcessedAt == null && DeadLetteredAt == null && (NextAttemptAt
        // == null || NextAttemptAt <= now), ORDER BY CreatedAt` TOP(batch) (OutboxProcessor).
        // A FILTERED index over just the pending set keeps the poll on the small hot set,
        // and leading with CreatedAt lets `ORDER BY CreatedAt … TOP` be an ordered scan that
        // short-circuits — rather than materialising + sorting the whole set. The
        // NextAttemptAt / ClaimExpiresAt due-checks are residual predicates on that scan.
        builder.HasIndex(x => x.CreatedAt)
            .HasDatabaseName($"IX_{ModuleTablePrefixes.Default}OutboxMessages_Dispatch")
            .HasFilter("[ProcessedAt] IS NULL AND [DeadLetteredAt] IS NULL");

        builder.HasIndex(x => x.EventId)
            .HasDatabaseName($"IX_{ModuleTablePrefixes.Default}OutboxMessages_EventId");
    }
}
