using Aonik.Finance.Entities.ExternalAccounts;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Finance.Persistence.Configurations.ExternalAccounts;

internal class ExternalAccountLinkedAccountConfiguration : IEntityTypeConfiguration<ExternalAccountLinkedAccount>
{
    public void Configure(EntityTypeBuilder<ExternalAccountLinkedAccount> builder)
    {
        builder.ToTable("ExternalAccountLinkedAccounts", SchemaNames.Default);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ProviderAccountReference)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.AccountType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.AccountSubtype)
            .HasMaxLength(50);

        builder.Property(x => x.Currency)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(x => x.Last4)
            .HasMaxLength(4);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.LastSyncStatus)
            .HasMaxLength(100);

        builder.Property(x => x.LastError)
            .HasMaxLength(500);

        builder.HasIndex(x => new { x.TenantId, x.ExternalAccountConnectionId, x.ProviderAccountReference })
            .IsUnique();

        builder.HasIndex(x => new { x.TenantId, x.ExternalAccountId });

        builder.HasOne<ExternalAccountConnection>()
            .WithMany()
            .HasForeignKey(x => x.ExternalAccountConnectionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
