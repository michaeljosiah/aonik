using Aonik.Platform.Entities.ContactEnquiries;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Platform.Persistence.Configurations.ContactEnquiries;

public class ContactEnquiryConfiguration : IEntityTypeConfiguration<ContactEnquiry>
{
    public void Configure(EntityTypeBuilder<ContactEnquiry> builder)
    {
        builder.HasKey(row => row.Id);
        builder.Property(row => row.SubmissionHash).HasMaxLength(64).IsRequired();
        builder.Property(row => row.Name).HasMaxLength(200).IsRequired();
        builder.Property(row => row.Email).HasMaxLength(254).IsRequired();
        builder.Property(row => row.Topic).HasMaxLength(16).IsRequired();
        builder.Property(row => row.OrderNumber).HasMaxLength(64);
        builder.Property(row => row.Message).HasMaxLength(5000).IsRequired();
        builder.Property(row => row.StaffRecipientEmail).HasMaxLength(254).IsRequired();
        builder.Property(row => row.StaffDetailUrl).HasMaxLength(2048).IsRequired();
        builder.HasIndex(row => new { row.TenantId, row.SubmissionId }).IsUnique();
        builder.HasIndex(row => new { row.TenantId, row.ReceivedAtUtc, row.Id });
        builder.HasMany(row => row.Images).WithOne().HasForeignKey(row => row.EnquiryId).OnDelete(DeleteBehavior.Cascade);
    }
}
