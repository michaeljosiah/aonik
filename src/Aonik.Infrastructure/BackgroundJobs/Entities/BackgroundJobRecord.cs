using System;

namespace Aonik.Infrastructure.BackgroundJobs.Entities;

/// <summary>
/// Represents a record of a background job in the database.
/// </summary>
public class BackgroundJobRecord
{
    /// <summary>
    /// Gets or sets the unique identifier of the job.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the name of the job type.
    /// </summary>
    public string JobName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the serialized arguments for the job.
    /// </summary>
    public string ArgumentsJson { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current status of the job.
    /// </summary>
    public string Status { get; set; } = "Enqueued";

    /// <summary>
    /// Gets or sets the number of retry attempts.
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of retries allowed.
    /// </summary>
    public int MaxRetryCount { get; set; } = 3;

    /// <summary>
    /// Gets or sets the priority of the job.
    /// </summary>
    public byte Priority { get; set; }

    /// <summary>
    /// Gets or sets when the job was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets when the job should be next attempted.
    /// </summary>
    public DateTime? NextAttemptAt { get; set; }

    /// <summary>
    /// Gets or sets when the job was last attempted.
    /// </summary>
    public DateTime? LastAttemptAt { get; set; }

    /// <summary>
    /// Gets or sets when the job completed (successfully or failed).
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Gets or sets the number of times the job has been tried.
    /// </summary>
    public int TryCount { get; set; }

    /// <summary>
    /// Gets or sets the error message if the job failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the serialized error details.
    /// </summary>
    public string? ErrorDetailsJson { get; set; }

    /// <summary>
    /// Gets or sets the tenant ID this job belongs to.
    /// </summary>
    public Guid? TenantId { get; set; }

    /// <summary>
    /// Gets or sets the correlation ID for tracing.
    /// </summary>
    public string? CorrelationId { get; set; }
}

/// <summary>
/// Defines possible job statuses.
/// </summary>
public static class JobStatus
{
    public const string Enqueued = "Enqueued";
    public const string Processing = "Processing";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
    public const string Cancelled = "Cancelled";
}
