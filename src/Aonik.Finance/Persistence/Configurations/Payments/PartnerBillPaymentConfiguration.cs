using Aonik.Finance.Entities.Partners;
using Aonik.Finance.Entities.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Finance.Persistence.Configurations.Payments;

internal class PartnerBillPaymentConfiguration : IEntityTypeConfiguration<PartnerBillPayment>
{
    public void Configure(EntityTypeBuilder<PartnerBillPayment> builder)
    {
        builder.ToTable("PartnerBillPayments");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.BillerCode).IsRequired().HasMaxLength(100);
        builder.Property(x => x.ItemCode).IsRequired().HasMaxLength(100);
        builder.Property(x => x.CustomerId).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Amount).IsRequired().HasPrecision(19, 4);
        builder.Property(x => x.Currency).IsRequired().HasMaxLength(3);
        builder.Property(x => x.ClientReference).IsRequired().HasMaxLength(200);
        builder.Property(x => x.ProviderReference).HasMaxLength(200);
        builder.Property(x => x.ServiceCategory).IsRequired().HasMaxLength(30);
        builder.Property(x => x.VendToken).HasMaxLength(200);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(50);
        builder.Property(x => x.FailureReason).HasMaxLength(1000);
        builder.Property(x => x.RawResponseJson).HasColumnType("nvarchar(max)");

        builder.HasIndex(x => new { x.TenantId, x.ClientReference }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.ProviderReference });
        builder.HasIndex(x => new { x.TenantId, x.OrderId });

        // OrderId / OrderItemId are soft references (linkage flows through OrderFulfilmentRef).
        builder.HasOne<Connector>().WithMany().HasForeignKey(x => x.ConnectorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ConnectorBillerMapping>().WithMany().HasForeignKey(x => x.ConnectorBillerMappingId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<BillValidation>().WithMany().HasForeignKey(x => x.BillValidationId).OnDelete(DeleteBehavior.Restrict);
    }
}
