using Aonik.Platform.Entities.ContactEnquiries;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Platform.Persistence.Configurations.ContactEnquiries;

public class ContactEnquiryAttachmentConfiguration : IEntityTypeConfiguration<ContactEnquiryAttachment>
{
    public void Configure(EntityTypeBuilder<ContactEnquiryAttachment> builder)
    {
        builder.HasKey(row => row.Id);
        builder.Property(row => row.FileName).HasMaxLength(200).IsRequired();
        builder.Property(row => row.ContentType).HasMaxLength(64).IsRequired();
        builder.Property(row => row.StorageKey).HasMaxLength(1024).IsRequired();
        builder.Property(row => row.Sha256).HasMaxLength(64).IsRequired();
        builder.HasIndex(row => new { row.TenantId, row.EnquiryId });
    }
}
