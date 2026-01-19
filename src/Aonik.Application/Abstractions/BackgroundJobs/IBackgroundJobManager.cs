using System;
using System.Threading.Tasks;

namespace Aonik.Application.Abstractions.BackgroundJobs;

/// <summary>
/// Defines the interface for managing background jobs.
/// </summary>
public interface IBackgroundJobManager
{
    /// <summary>
    /// Enqueues a job to be executed asynchronously.
    /// </summary>
    /// <typeparam name="TArgs">The type of job arguments.</typeparam>
    /// <param name="args">The job arguments.</param>
    /// <param name="priority">The job priority (default: Normal).</param>
    /// <param name="delay">Optional delay before the job is executed.</param>
    /// <returns>A unique identifier for the enqueued job.</returns>
    Task<string> EnqueueAsync<TArgs>(
        TArgs args,
        BackgroundJobPriority priority = BackgroundJobPriority.Normal,
        TimeSpan? delay = null) where TArgs : class;

    /// <summary>
    /// Enqueues a job with retry configuration.
    /// </summary>
    /// <typeparam name="TArgs">The type of job arguments.</typeparam>
    /// <param name="args">The job arguments.</param>
    /// <param name="retryCount">Number of retry attempts on failure.</param>
    /// <param name="retryInterval">Interval between retry attempts.</param>
    /// <param name="priority">The job priority (default: Normal).</param>
    /// <param name="delay">Optional delay before the first execution.</param>
    /// <returns>A unique identifier for the enqueued job.</returns>
    Task<string> EnqueueWithRetryAsync<TArgs>(
        TArgs args,
        int retryCount,
        TimeSpan retryInterval,
        BackgroundJobPriority priority = BackgroundJobPriority.Normal,
        TimeSpan? delay = null) where TArgs : class;
}
