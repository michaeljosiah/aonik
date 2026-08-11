using Aonik.Finance.Entities.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Finance.Persistence.Configurations;

internal sealed class PaymentMandateConfiguration : IEntityTypeConfiguration<PaymentMandate>
{
    public void Configure(EntityTypeBuilder<PaymentMandate> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Provider).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(32);
        builder.Property(x => x.ProviderMandateRef).HasMaxLength(200);
        builder.Property(x => x.RevocationReason).HasMaxLength(500);

        // Resolving a party's chargeable mandate is the hot path — every renewal does it.
        builder.HasIndex(x => new { x.TenantId, x.PartyId, x.Status });

        // A revoked card's mandate must be findable when the provider notifies us out of band.
        builder.HasIndex(x => new { x.TenantId, x.PaymentMethodId });
    }
}
