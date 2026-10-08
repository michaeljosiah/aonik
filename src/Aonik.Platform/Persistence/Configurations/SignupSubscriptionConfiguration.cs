using Aonik.Platform.Entities.SignupLists;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Platform.Persistence.Configurations;

public class SignupSubscriptionConfiguration : IEntityTypeConfiguration<SignupSubscription>
{
    public void Configure(EntityTypeBuilder<SignupSubscription> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ListType).IsRequired().HasMaxLength(32);
        builder.Property(x => x.Email).IsRequired().HasMaxLength(254);
        builder.Property(x => x.NormalizedEmail).IsRequired().HasMaxLength(254);
        builder.Property(x => x.Postcode).HasMaxLength(8);
        builder.Property(x => x.PostcodeOutwardCode).HasMaxLength(4);
        builder.Property(x => x.Name).HasMaxLength(200);
        builder.Property(x => x.Phone).HasMaxLength(50);
        builder.Property(x => x.CountryCode).HasMaxLength(2);
        builder.Property(x => x.Service).HasMaxLength(64);
        builder.Property(x => x.ConsentVersion).IsRequired().HasMaxLength(32);
        builder.Property(x => x.ConsentTextSnapshot).IsRequired().HasMaxLength(1000);
        builder.Property(x => x.ConsentSource).IsRequired().HasMaxLength(64);
        builder.HasIndex(x => new { x.TenantId, x.ListType, x.NormalizedEmail })
            .IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(x => new { x.TenantId, x.ListType, x.UnsubscribedAtUtc });
    }
}
