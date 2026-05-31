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

        // Two filtered unique indexes dedupe events: SQL Server treats NULLs as equal in a unique
        // index, so one unfiltered index over the nullable ProviderEventId would reject every
        // event-id-less row. Primary key is (ProviderCode, ProviderEventId) when present; the
        // payload-hash fallback applies only when ProviderEventId is null.
        builder.HasIndex(x => new { x.ProviderCode, x.ProviderEventId })
            .IsUnique()
            .HasFilter("[ProviderEventId] IS NOT NULL")
            .HasDatabaseName("UX_PartnerWebhookEvents_ProviderCode_ProviderEventId");

        builder.HasIndex(x => new { x.ProviderCode, x.PayloadHash })
            .IsUnique()
            .HasFilter("[ProviderEventId] IS NULL")
            .HasDatabaseName("UX_PartnerWebhookEvents_ProviderCode_PayloadHash");

        builder.HasIndex(x => x.ClientReference);
        builder.HasIndex(x => x.ProviderReference);
    }
}
