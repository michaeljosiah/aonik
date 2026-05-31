using Aonik.Finance.Entities.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Finance.Persistence.Configurations.Payments;

internal class PayoutConfiguration : IEntityTypeConfiguration<Payout>
{
    public void Configure(EntityTypeBuilder<Payout> builder)
    {
        builder.ToTable("Payouts");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Amount).IsRequired().HasPrecision(19, 4);
        builder.Property(x => x.Currency).IsRequired().HasMaxLength(3);
        builder.Property(x => x.ClientReference).IsRequired().HasMaxLength(200);
        builder.Property(x => x.ProviderReference).HasMaxLength(200);
        builder.Property(x => x.DebitCurrency).HasMaxLength(3);
        builder.Property(x => x.FxRate).HasPrecision(19, 8);
        builder.Property(x => x.ConvertedAmount).HasPrecision(19, 4);
        builder.Property(x => x.Fee).HasPrecision(19, 4);
        builder.Property(x => x.FeeCurrency).HasMaxLength(3);
        builder.Property(x => x.Narration).HasMaxLength(500);
        builder.Property(x => x.DestinationType).HasMaxLength(30);
        builder.Property(x => x.FailureReason).HasMaxLength(1000);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(50);
        builder.Property(x => x.RawResponseJson).HasColumnType("nvarchar(max)");

        // ClientReference is our idempotency key, but the index stays non-unique: the column is new on an
        // existing table and back-fills to '' for any legacy row, which a unique index would reject.
        builder.HasIndex(x => new { x.TenantId, x.ClientReference });
        builder.HasIndex(x => new { x.TenantId, x.ProviderReference });
        builder.HasIndex(x => new { x.TenantId, x.Status });
        builder.HasIndex(x => new { x.TenantId, x.ConnectorId });

        // ConnectorId / PartnerId stay soft references (no FK): ConnectorId is non-nullable and would fail
        // an FK check for any legacy payout predating the connector catalog. DestinationExternalAccountId is
        // the one structured FK - to the new ExternalPayoutAccounts table (the destination's home).
        builder.HasOne<ExternalPayoutAccount>().WithMany().HasForeignKey(x => x.DestinationExternalAccountId).OnDelete(DeleteBehavior.Restrict);
    }
}
