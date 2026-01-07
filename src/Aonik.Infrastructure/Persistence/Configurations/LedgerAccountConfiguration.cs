using Aonik.Domain.Ledger.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Infrastructure.Persistence.Configurations;

public class LedgerAccountConfiguration : IEntityTypeConfiguration<LedgerAccount>
{
    public void Configure(EntityTypeBuilder<LedgerAccount> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Currency)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(x => x.CreatedUtc)
            .IsRequired();

        builder.HasIndex(x => x.Name);
    }
}
