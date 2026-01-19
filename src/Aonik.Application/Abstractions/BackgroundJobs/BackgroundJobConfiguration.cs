using System;

namespace Aonik.Application.Abstractions.BackgroundJobs;

/// <summary>
/// Represents configuration for a background job.
/// </summary>
public class BackgroundJobConfiguration
{
    /// <summary>
    /// Gets the type of the job.
    /// </summary>
    public Type JobType { get; }

    /// <summary>
    /// Gets the type of arguments the job accepts.
    /// </summary>
    public Type ArgsType { get; }

    /// <summary>
    /// Gets the unique name of the job.
    /// </summary>
    public string JobName { get; }

    /// <summary>
    /// Gets or sets the maximum number of retry attempts.
    /// </summary>
    public int MaxRetryCount { get; set; } = 3;

    /// <summary>
    /// Gets or sets the default retry interval.
    /// </summary>
    public TimeSpan DefaultRetryInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Creates a new instance of <see cref="BackgroundJobConfiguration"/>
    /// </summary>
    /// <param name="jobType">The type of the job class.</param>
    /// <param name="jobName">The unique name of the job.</param>
    public BackgroundJobConfiguration(Type jobType, string jobName)
    {
        JobType = jobType ?? throw new ArgumentNullException(nameof(jobType));
        JobName = jobName ?? throw new ArgumentNullException(nameof(jobName));
        
        var interfaceType = jobType.GetInterface(typeof(IBackgroundJob<>).Name) 
                         ?? jobType.GetInterface(typeof(IAsyncBackgroundJob<>).Name);
        
        if (interfaceType == null)
        {
            throw new ArgumentException(
                $"The job type {jobType.Name} must implement IBackgroundJob<TArgs> or IAsyncBackgroundJob<TArgs>",
                nameof(jobType));
        }

        ArgsType = interfaceType.GetGenericArguments()[0];
    }
}
