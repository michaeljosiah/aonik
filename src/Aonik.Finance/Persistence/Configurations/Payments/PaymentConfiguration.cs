using Aonik.Finance.Entities.Payments;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Finance.Persistence.Configurations.Payments;

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.HasKey(payment => payment.Id);
        builder.Property(payment => payment.Provider).HasMaxLength(50);
        builder.Property(payment => payment.ProviderReference).HasMaxLength(200);
        builder.Property(payment => payment.OutcomeStatus).HasMaxLength(50);
        builder.Property(payment => payment.Amount).HasPrecision(19, 4);
        builder.Property(payment => payment.Currency).HasMaxLength(3);
        builder.HasIndex(payment => new { payment.TenantId, payment.PaymentIntentId })
            .IsUnique().HasFilter("[ConnectorId] IS NOT NULL");
        builder.HasIndex(payment => new { payment.ConnectorId, payment.ProviderReference })
            .IsUnique().HasFilter("[ConnectorId] IS NOT NULL AND [ProviderReference] IS NOT NULL");
    }
}
