using Aonik.Finance.Entities.ExternalAccounts;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Finance.Persistence.Configurations.ExternalAccounts;

internal class ExternalAccountConnectionSessionConfiguration : IEntityTypeConfiguration<ExternalAccountConnectionSession>
{
    public void Configure(EntityTypeBuilder<ExternalAccountConnectionSession> builder)
    {
        builder.ToTable("ExternalAccountConnectionSessions", SchemaNames.Default);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Provider)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Mode)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.SessionToken)
            .IsRequired()
            .HasMaxLength(400);

        builder.Property(x => x.ProviderSessionReference)
            .HasMaxLength(200);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(x => x.ExternalAccountConnectionId);

        builder.HasIndex(x => x.SessionToken)
            .IsUnique();

        builder.HasIndex(x => new { x.TenantId, x.UserId, x.Provider, x.Status });
    }
}
