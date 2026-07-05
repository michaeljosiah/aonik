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

        // Hot path (M8): the processor polls for unprocessed, non-dead-lettered, due rows
        // oldest-first (OutboxProcessor: ProcessedAt == null && DeadLetteredAt == null &&
        // (NextAttemptAt == null || NextAttemptAt <= now), ORDER BY CreatedAt). A FILTERED
        // index over just the pending set moves the two NULL conditions into the filter so
        // the poll touches only the small hot set of undispatched rows rather than the whole
        // (mostly-processed) table.
        builder.HasIndex(x => new { x.NextAttemptAt, x.CreatedAt })
            .HasDatabaseName($"IX_{ModuleTablePrefixes.Default}OutboxMessages_Dispatch")
            .HasFilter("[ProcessedAt] IS NULL AND [DeadLetteredAt] IS NULL");

        builder.HasIndex(x => x.EventId)
            .HasDatabaseName($"IX_{ModuleTablePrefixes.Default}OutboxMessages_EventId");
    }
}
