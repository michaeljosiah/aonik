using System;
using System.Threading;

namespace Aonik.Application.Abstractions.BackgroundJobs;

/// <summary>
/// Represents the context in which a background job is executed.
/// </summary>
public class JobExecutionContext
{
    /// <summary>
    /// Gets the service provider for resolving dependencies.
    /// </summary>
    public IServiceProvider ServiceProvider { get; }

    /// <summary>
    /// Gets the type of the job being executed.
    /// </summary>
    public Type JobType { get; }

    /// <summary>
    /// Gets the arguments passed to the job.
    /// </summary>
    public object JobArgs { get; }

    /// <summary>
    /// Gets the cancellation token for the job execution.
    /// </summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// Creates a new instance of <see cref="JobExecutionContext"/>
    /// </summary>
    /// <param name="serviceProvider">The service provider for dependency resolution.</param>
    /// <param name="jobType">The type of the job being executed.</param>
    /// <param name="jobArgs">The arguments passed to the job.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public JobExecutionContext(
        IServiceProvider serviceProvider,
        Type jobType,
        object jobArgs,
        CancellationToken cancellationToken = default)
    {
        ServiceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        JobType = jobType ?? throw new ArgumentNullException(nameof(jobType));
        JobArgs = jobArgs ?? throw new ArgumentNullException(nameof(jobArgs));
        CancellationToken = cancellationToken;
    }
}
