using System;

namespace Aonik.Application.Abstractions.BackgroundJobs;

/// <summary>
/// Represents an exception that occurs during background job execution.
/// </summary>
public class BackgroundJobExecutionException : Exception
{
    /// <summary>
    /// Gets or sets the type of the job that failed.
    /// </summary>
    public string? JobType { get; set; }

    /// <summary>
    /// Gets or sets the serialized arguments of the failed job.
    /// </summary>
    public string? JobArgs { get; set; }

    /// <summary>
    /// Gets or sets the retry attempt number.
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// Gets or sets whether this exception can be retried.
    /// </summary>
    public bool CanRetry { get; set; }

    /// <summary>
    /// Creates a new instance of <see cref="BackgroundJobExecutionException"/>
    /// </summary>
    public BackgroundJobExecutionException()
    {
    }

    /// <summary>
    /// Creates a new instance of <see cref="BackgroundJobExecutionException"/>
    /// </summary>
    /// <param name="message">The error message.</param>
    public BackgroundJobExecutionException(string message) : base(message)
    {
    }

    /// <summary>
    /// Creates a new instance of <see cref="BackgroundJobExecutionException"/>
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public BackgroundJobExecutionException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
