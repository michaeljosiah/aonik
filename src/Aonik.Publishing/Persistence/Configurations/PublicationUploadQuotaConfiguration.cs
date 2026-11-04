using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Aonik.Publishing.Entities;

namespace Aonik.Publishing.Persistence.Configurations;

public sealed class PublicationUploadQuotaConfiguration : IEntityTypeConfiguration<PublicationUploadQuota>
{
    public void Configure(EntityTypeBuilder<PublicationUploadQuota> b)
    {
        b.ToTable("AnkPublicationUploadQuotas", "dbo"); b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.TenantId, x.OwnerPartyId }).IsUnique();
    }
}
