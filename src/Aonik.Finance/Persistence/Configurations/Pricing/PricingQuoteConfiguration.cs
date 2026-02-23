using Aonik.Finance.Entities.Pricing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Finance.Persistence.Configurations;

public class PricingQuoteConfiguration : IEntityTypeConfiguration<PricingQuote>
{
    public void Configure(EntityTypeBuilder<PricingQuote> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.QuoteType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.OriginCurrency)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(x => x.DestinationCurrency)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(x => x.OriginCountry)
            .IsRequired()
            .HasMaxLength(2);

        builder.Property(x => x.DestinationCountry)
            .IsRequired()
            .HasMaxLength(2);

        builder.Property(x => x.ServiceCode)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.OriginAmount)
            .HasPrecision(19, 4);

        builder.Property(x => x.DestinationAmount)
            .HasPrecision(19, 4);

        builder.Property(x => x.ExchangeRate)
            .HasPrecision(19, 8);

        builder.Property(x => x.RateMarkup)
            .HasPrecision(19, 8);

        builder.Property(x => x.FeesTotal)
            .HasPrecision(19, 4);

        builder.Property(x => x.TotalAmount)
            .HasPrecision(19, 4);

        builder.Property(x => x.PricingPolicyVersion)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.FxRateProvider)
            .HasMaxLength(100);

        builder.Property(x => x.CustomerTier)
            .HasMaxLength(50);

        builder.Property(x => x.QuoteContext)
            .HasMaxLength(100);

        builder.Property(x => x.FeeBreakdownJson)
            .HasColumnType("nvarchar(max)");

        builder.HasIndex(x => x.QuoteType);
        builder.HasIndex(x => x.ServiceCode);
        builder.HasIndex(x => x.ExpiresAt);
        builder.HasIndex(x => x.CustomerId);
    }
}
