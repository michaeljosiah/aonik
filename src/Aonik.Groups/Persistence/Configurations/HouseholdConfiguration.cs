using Aonik.PersonalFinance.Entities;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.PersonalFinance.Persistence.Configurations;

/// <summary>
/// Spec 086 §10.2. Household had no configuration of its own until now — only a table mapping — so
/// this class exists solely to constrain the new <see cref="Household.Kind"/> discriminator. The
/// pre-existing columns are deliberately left to convention: giving them lengths here would scaffold
/// AlterColumn operations that have nothing to do with this extraction.
/// </summary>
internal class HouseholdConfiguration : IEntityTypeConfiguration<Household>
{
    public void Configure(EntityTypeBuilder<Household> builder)
    {
        builder.ToTable("Households", SchemaNames.Default);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Kind).IsRequired().HasMaxLength(32);
    }
}
