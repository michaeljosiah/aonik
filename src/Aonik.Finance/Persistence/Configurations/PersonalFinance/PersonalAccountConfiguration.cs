using Aonik.Finance.Entities.PersonalFinance;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Finance.Persistence.Configurations.PersonalFinance;

internal class PersonalAccountConfiguration : IEntityTypeConfiguration<PersonalAccount>
{
    public void Configure(EntityTypeBuilder<PersonalAccount> builder)
    {
        builder.ToTable("PersonalAccounts", SchemaNames.Default);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.AccountType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Currency)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(x => x.InstitutionName)
            .HasMaxLength(200);

        builder.Property(x => x.ExternalReference)
            .HasMaxLength(200);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.AccountSubtype)
            .HasMaxLength(50);

        builder.Property(x => x.Last4)
            .HasMaxLength(4);

        builder.Property(x => x.IsArchived)
            .HasDefaultValue(false);

        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.HouseholdId);
        builder.HasIndex(x => x.ExternalReference);
        builder.HasIndex(x => new { x.TenantId, x.UserId, x.IsArchived });
    }
}
