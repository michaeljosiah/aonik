using System;

namespace Aonik.Infrastructure.BackgroundJobs.Quartz;

/// <summary>
/// Configuration options for Quartz background jobs.
/// </summary>
public class QuartzBackgroundJobOptions
{
    /// <summary>
    /// Gets or sets the default number of retry attempts.
    /// Default: 3.
    /// </summary>
    public int DefaultRetryCount { get; set; } = 3;

    /// <summary>
    /// Gets or sets the default retry interval in milliseconds.
    /// Default: 60000 (1 minute).
    /// </summary>
    public int DefaultRetryIntervalMilliseconds { get; set; } = 60000;

    /// <summary>
    /// Gets or sets whether to use concurrent execution of jobs.
    /// Default: false (jobs execute sequentially).
    /// </summary>
    public bool AllowConcurrentExecution { get; set; } = false;

    /// <summary>
    /// Gets or sets the maximum number of concurrent jobs.
    /// Default: 1.
    /// </summary>
    public int MaxConcurrentJobs { get; set; } = 1;

    /// <summary>
    /// Gets or sets the Quartz thread pool name.
    /// Default: "AonikBackgroundJobs".
    /// </summary>
    public string ThreadPoolName { get; set; } = "AonikBackgroundJobs";

    /// <summary>
    /// Gets or sets whether to auto-start the scheduler.
    /// Default: true.
    /// </summary>
    public bool AutoStartScheduler { get; set; } = true;

    /// <summary>
    /// Gets or sets the shutdown timeout for the Quartz scheduler.
    /// Default: 30 seconds.
    /// </summary>
    public TimeSpan ShutdownTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
