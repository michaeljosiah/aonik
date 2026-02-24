using Aonik.Finance.Entities.PersonalFinance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Infrastructure.Persistence.Configurations;

public class PersonalAccountConfiguration : IEntityTypeConfiguration<PersonalAccount>
{
    public void Configure(EntityTypeBuilder<PersonalAccount> builder)
    {
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

        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.HouseholdId);
        builder.HasIndex(x => x.ExternalReference);
    }
}
