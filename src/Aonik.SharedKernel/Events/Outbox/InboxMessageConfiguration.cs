using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.SharedKernel.Events.Outbox;

public sealed class InboxMessageConfiguration : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> builder)
    {
        builder.ToTable($"{ModuleTablePrefixes.Default}InboxMessages", SchemaNames.Default);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.EventId).IsRequired();
        builder.Property(x => x.HandlerName).IsRequired().HasMaxLength(512);

        // One processing record per (event, handler) — enforces idempotent redelivery.
        builder.HasIndex(x => new { x.EventId, x.HandlerName })
            .IsUnique()
            .HasDatabaseName($"UX_{ModuleTablePrefixes.Default}InboxMessages_Event_Handler");
    }
}
