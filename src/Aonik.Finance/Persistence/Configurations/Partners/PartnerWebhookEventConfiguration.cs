using Aonik.Finance.Entities.Partners;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Finance.Persistence.Configurations.Partners;

internal class PartnerWebhookEventConfiguration : IEntityTypeConfiguration<PartnerWebhookEvent>
{
    public void Configure(EntityTypeBuilder<PartnerWebhookEvent> builder)
    {
        builder.ToTable("PartnerWebhookEvents");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ProviderCode).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Category).IsRequired().HasMaxLength(30);
        builder.Property(x => x.EventType).IsRequired().HasMaxLength(100);
        builder.Property(x => x.ProviderEventId).HasMaxLength(200);
        builder.Property(x => x.ProviderReference).HasMaxLength(200);
        builder.Property(x => x.ClientReference).HasMaxLength(200);
        builder.Property(x => x.PayloadHash).IsRequired().HasMaxLength(128);
        builder.Property(x => x.RawPayload).HasColumnType("nvarchar(max)");
        builder.Property(x => x.ProcessingStatus).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Error).HasMaxLength(2000);

        // Connector-aware dedupe (Spec 042 §9.2). Once an event resolves to a connector (signature verified),
        // its dedupe bucket is keyed by ConnectorId so two connector instances of the same provider can never
        // alias. Events not yet resolved to a connector (ConnectorId NULL — rejected or untranslatable rows)
        // fall back to the provider-code bucket. Each bucket splits on ProviderEventId presence because
        // SQL Server treats NULLs as equal in a unique index, so an event-id-less row must not collide.
        builder.HasIndex(x => new { x.ConnectorId, x.ProviderEventId })
            .IsUnique()
            .HasFilter("[ConnectorId] IS NOT NULL AND [ProviderEventId] IS NOT NULL")
            .HasDatabaseName("UX_PartnerWebhookEvents_Connector_ProviderEventId");

        builder.HasIndex(x => new { x.ConnectorId, x.PayloadHash })
            .IsUnique()
            .HasFilter("[ConnectorId] IS NOT NULL AND [ProviderEventId] IS NULL")
            .HasDatabaseName("UX_PartnerWebhookEvents_Connector_PayloadHash");

        builder.HasIndex(x => new { x.ProviderCode, x.ProviderEventId })
            .IsUnique()
            .HasFilter("[ConnectorId] IS NULL AND [ProviderEventId] IS NOT NULL")
            .HasDatabaseName("UX_PartnerWebhookEvents_ProviderCode_ProviderEventId");

        builder.HasIndex(x => new { x.ProviderCode, x.PayloadHash })
            .IsUnique()
            .HasFilter("[ConnectorId] IS NULL AND [ProviderEventId] IS NULL")
            .HasDatabaseName("UX_PartnerWebhookEvents_ProviderCode_PayloadHash");

        builder.HasIndex(x => x.ConnectorId);
        builder.HasIndex(x => x.ClientReference);
        builder.HasIndex(x => x.ProviderReference);
    }
}
