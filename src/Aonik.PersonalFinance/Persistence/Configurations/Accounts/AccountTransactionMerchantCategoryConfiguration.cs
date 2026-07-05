using Aonik.Finance.Entities.Accounts;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Finance.Persistence.Configurations.Accounts;

internal class AccountTransactionMerchantCategoryConfiguration
    : IEntityTypeConfiguration<AccountTransactionMerchantCategory>
{
    public void Configure(EntityTypeBuilder<AccountTransactionMerchantCategory> builder)
    {
        builder.ToTable("AccountTransactionMerchantCategories", SchemaNames.Default);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.MerchantKey)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Category)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.SubCategory)
            .HasMaxLength(100);

        builder.HasIndex(x => new { x.TenantId, x.MerchantKey })
            .IsUnique();
    }
}
