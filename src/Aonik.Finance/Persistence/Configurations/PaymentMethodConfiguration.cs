using Aonik.Finance.Entities.Payments;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Finance.Persistence.Configurations;

public class PaymentMethodConfiguration : IEntityTypeConfiguration<PaymentMethod>
{
    public void Configure(EntityTypeBuilder<PaymentMethod> builder)
    {
        builder.ToTable("PaymentMethods", SchemaNames.Default);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.CustomerPartyId)
            .IsRequired();

        builder.Property(x => x.Provider)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.ProviderToken)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.ProviderCustomerRef)
            .HasMaxLength(200);

        builder.Property(x => x.Type)
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(x => x.Brand)
            .HasMaxLength(30);

        // Last four digits only — the column is deliberately too small to hold a PAN.
        builder.Property(x => x.Last4)
            .HasMaxLength(4);

        builder.Property(x => x.Label)
            .HasMaxLength(100);

        builder.HasIndex(x => new { x.TenantId, x.CustomerPartyId });

        // Idempotency guard: a given provider token is vaulted at most once per customer. The unique
        // index is the authority under concurrency (the read-before-insert in the service is only the
        // happy path). Filtered on IsDeleted so a soft-deleted row never blocks re-saving the same
        // token (AonikDbContextBase turns Remove into a soft-delete).
        builder.HasIndex(x => new { x.TenantId, x.CustomerPartyId, x.Provider, x.ProviderToken })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        // At most one default per customer — the single-default contract holds even under concurrent
        // inserts (two racing new cards can't both win the default). Filtered to default, non-deleted rows.
        builder.HasIndex(x => new { x.TenantId, x.CustomerPartyId })
            .IsUnique()
            .HasFilter("[IsDefault] = 1 AND [IsDeleted] = 0")
            .HasDatabaseName("UX_PaymentMethods_OneDefaultPerCustomer");
    }
}
