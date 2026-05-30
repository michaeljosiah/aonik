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

        // Hot path: the processor polls for unprocessed, non-dead-lettered, due rows oldest-first.
        builder.HasIndex(x => new { x.ProcessedAt, x.DeadLetteredAt, x.NextAttemptAt, x.CreatedAt })
            .HasDatabaseName($"IX_{ModuleTablePrefixes.Default}OutboxMessages_Dispatch");

        builder.HasIndex(x => x.EventId)
            .HasDatabaseName($"IX_{ModuleTablePrefixes.Default}OutboxMessages_EventId");
    }
}
