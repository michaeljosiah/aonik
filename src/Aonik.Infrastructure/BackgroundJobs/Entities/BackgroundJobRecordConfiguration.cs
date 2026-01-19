using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Infrastructure.BackgroundJobs.Entities;

/// <summary>
/// Entity configuration for <see cref="BackgroundJobRecord"/>.
/// </summary>
public class BackgroundJobRecordConfiguration : IEntityTypeConfiguration<BackgroundJobRecord>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<BackgroundJobRecord> builder)
    {
        builder.ToTable("AonikBackgroundJobRecords");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("Id")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.JobName)
            .HasColumnName("JobName")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(x => x.ArgumentsJson)
            .HasColumnName("ArgumentsJson")
            .IsRequired();

        builder.Property(x => x.Status)
            .HasColumnName("Status")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.RetryCount)
            .HasColumnName("RetryCount");

        builder.Property(x => x.MaxRetryCount)
            .HasColumnName("MaxRetryCount");

        builder.Property(x => x.Priority)
            .HasColumnName("Priority");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("CreatedAt")
            .IsRequired();

        builder.Property(x => x.NextAttemptAt)
            .HasColumnName("NextAttemptAt");

        builder.Property(x => x.LastAttemptAt)
            .HasColumnName("LastAttemptAt");

        builder.Property(x => x.CompletedAt)
            .HasColumnName("CompletedAt");

        builder.Property(x => x.TryCount)
            .HasColumnName("TryCount");

        builder.Property(x => x.ErrorMessage)
            .HasColumnName("ErrorMessage")
            .HasMaxLength(2048);

        builder.Property(x => x.ErrorDetailsJson)
            .HasColumnName("ErrorDetailsJson");

        builder.Property(x => x.TenantId)
            .HasColumnName("TenantId");

        builder.Property(x => x.CorrelationId)
            .HasColumnName("CorrelationId")
            .HasMaxLength(200);

        // Indexes for efficient querying
        builder.HasIndex(x => x.Status)
            .HasDatabaseName("IX_BackgroundJobRecords_Status");

        builder.HasIndex(x => x.NextAttemptAt)
            .HasDatabaseName("IX_BackgroundJobRecords_NextAttemptAt");

        builder.HasIndex(x => x.Priority)
            .HasDatabaseName("IX_BackgroundJobRecords_Priority");

        builder.HasIndex(x => x.TenantId)
            .HasDatabaseName("IX_BackgroundJobRecords_TenantId");
    }
}
