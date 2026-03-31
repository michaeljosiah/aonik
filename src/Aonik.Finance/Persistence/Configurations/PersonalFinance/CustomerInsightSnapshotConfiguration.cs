using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Entities.PersonalFinance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Finance.Persistence.Configurations.PersonalFinance;

internal sealed class CustomerInsightSnapshotConfiguration : IEntityTypeConfiguration<CustomerInsightSnapshot>
{
    public void Configure(EntityTypeBuilder<CustomerInsightSnapshot> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(x => x.SourceHash)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(x => x.SnapshotJson)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.GeneratedBy)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(x => x.FailureReason)
            .HasMaxLength(1000);

        builder.HasOne<CustomerInsightSnapshot>()
            .WithMany()
            .HasForeignKey(x => x.SupersededById)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => new { x.TenantId, x.UserId, x.Status })
            .HasFilter($"[Status] = '{CustomerInsightSnapshotContract.StatusCurrent}'")
            .HasDatabaseName("IX_CustomerInsightSnapshots_TenantUser_Current");

        builder.HasIndex(x => new { x.TenantId, x.UserId, x.AsOfUtc })
            .HasDatabaseName("IX_CustomerInsightSnapshots_TenantUser_AsOfUtc");

        builder.HasIndex(x => new { x.TenantId, x.UserId, x.SourceHash })
            .HasDatabaseName("IX_CustomerInsightSnapshots_TenantUser_SourceHash");

        builder.HasIndex(x => x.SupersededById)
            .HasDatabaseName("IX_CustomerInsightSnapshots_SupersededById");
    }
}
