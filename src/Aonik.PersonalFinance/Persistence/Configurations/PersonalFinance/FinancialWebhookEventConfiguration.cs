using Aonik.PersonalFinance.Entities;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.PersonalFinance.Persistence.Configurations;

internal class FinancialWebhookEventConfiguration : IEntityTypeConfiguration<FinancialWebhookEvent>
{
    public void Configure(EntityTypeBuilder<FinancialWebhookEvent> builder)
    {
        builder.ToTable("FinancialWebhookEvents", SchemaNames.Default);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Provider)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.ProviderConnectionReference)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.ProviderEventType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.ProviderEventCode)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.ProcessingStatus)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.PayloadJson)
            .IsRequired();

        builder.Property(x => x.Error)
            .HasMaxLength(1000);

        builder.HasIndex(x => new { x.Provider, x.ProviderConnectionReference, x.ReceivedAt });
        builder.HasIndex(x => new { x.Provider, x.ProviderEventType, x.ProviderEventCode, x.ReceivedAt });

        builder.HasOne<FinancialConnection>()
            .WithMany()
            .HasForeignKey(x => x.FinancialConnectionId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
