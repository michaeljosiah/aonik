using Aonik.Finance.Entities.Partners;
using Aonik.Finance.Entities.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Finance.Persistence.Configurations.Payments;

internal class ExternalPayoutAccountConfiguration : IEntityTypeConfiguration<ExternalPayoutAccount>
{
    public void Configure(EntityTypeBuilder<ExternalPayoutAccount> builder)
    {
        builder.ToTable("ExternalPayoutAccounts");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.DestinationType).IsRequired().HasMaxLength(30);
        builder.Property(x => x.BankCode).HasMaxLength(35);
        builder.Property(x => x.BranchCode).HasMaxLength(35);
        builder.Property(x => x.MobileNetwork).HasMaxLength(50);
        builder.Property(x => x.MaskedAccountIdentifier).IsRequired().HasMaxLength(64);
        builder.Property(x => x.RailFingerprint).HasMaxLength(128);
        builder.Property(x => x.AccountName).IsRequired().HasMaxLength(256);
        builder.Property(x => x.Currency).IsRequired().HasMaxLength(3);
        builder.Property(x => x.ProviderCode).HasMaxLength(64);
        builder.Property(x => x.ProviderBeneficiaryId).HasMaxLength(200);
        builder.Property(x => x.VaultRef).HasMaxLength(200);

        builder.HasIndex(x => new { x.TenantId, x.BeneficiaryPartyId });
        builder.HasIndex(x => new { x.TenantId, x.ConnectorId });
        // The owning-customer scope: every rail read/write filters by (tenant, customer) so a shared
        // beneficiary party never lets one customer reach another's rails.
        builder.HasIndex(x => new { x.TenantId, x.CustomerPartyId, x.BeneficiaryPartyId });
        builder.HasIndex(x => new { x.TenantId, x.CustomerPartyId, x.ProviderCode, x.RailFingerprint })
            .IsUnique()
            .HasFilter("[ProviderCode] IS NOT NULL AND [RailFingerprint] IS NOT NULL AND [IsDeleted] = 0");

        // PartnerId / ConnectorId are real Finance FKs; BeneficiaryPartyId is a soft Guid
        // reference to the Platform Party (cross-module), so it gets no FK.
        builder.HasOne<Partner>().WithMany().HasForeignKey(x => x.PartnerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Connector>().WithMany().HasForeignKey(x => x.ConnectorId).OnDelete(DeleteBehavior.Restrict);
    }
}
