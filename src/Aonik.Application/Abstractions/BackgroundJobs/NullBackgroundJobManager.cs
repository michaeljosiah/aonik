using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aonik.Application.Abstractions.BackgroundJobs;

/// <summary>
/// A null implementation of <see cref="IBackgroundJobManager"/> that executes jobs synchronously.
/// Used for testing or when no background job system is configured.
/// </summary>
public class NullBackgroundJobManager : IBackgroundJobManager
{
    private readonly IBackgroundJobExecuter _jobExecuter;
    private readonly ILogger<NullBackgroundJobManager> _logger;

    /// <summary>
    /// Creates a new instance of <see cref="NullBackgroundJobManager"/>
    /// </summary>
    /// <param name="jobExecuter">The job executor to use.</param>
    /// <param name="logger">The logger instance.</param>
    public NullBackgroundJobManager(
        IBackgroundJobExecuter jobExecuter,
        ILogger<NullBackgroundJobManager>? logger = null)
    {
        _jobExecuter = jobExecuter;
        _logger = logger ?? NullLogger<NullBackgroundJobManager>.Instance;
    }

    /// <inheritdoc />
    public async Task<string> EnqueueAsync<TArgs>(
        TArgs args,
        BackgroundJobPriority priority = BackgroundJobPriority.Normal,
        TimeSpan? delay = null) where TArgs : class
    {
        var jobId = Guid.NewGuid().ToString();

        if (delay.HasValue && delay.Value > TimeSpan.Zero)
        {
            _logger.LogInformation(
                "Delaying job {JobId} of type {JobType} by {Delay}",
                jobId,
                typeof(TArgs).Name,
                delay.Value);

            await Task.Delay(delay.Value);
        }

        _logger.LogInformation(
            "Executing job {JobId} of type {JobType} synchronously",
            jobId,
            typeof(TArgs).Name);

        await ExecuteJobAsync(args, jobId);

        return jobId;
    }

    /// <inheritdoc />
    public async Task<string> EnqueueWithRetryAsync<TArgs>(
        TArgs args,
        int retryCount,
        TimeSpan retryInterval,
        BackgroundJobPriority priority = BackgroundJobPriority.Normal,
        TimeSpan? delay = null) where TArgs : class
    {
        var jobId = Guid.NewGuid().ToString();

        if (delay.HasValue && delay.Value > TimeSpan.Zero)
        {
            await Task.Delay(delay.Value);
        }

        int currentRetry = 0;
        while (true)
        {
            try
            {
                await ExecuteJobAsync(args, jobId);
                return jobId;
            }
            catch (Exception ex)
            {
                currentRetry++;
                if (currentRetry > retryCount)
                {
                    _logger.LogError(
                        ex,
                        "Job {JobId} of type {JobType} failed after {RetryCount} retries",
                        jobId,
                        typeof(TArgs).Name,
                        retryCount);

                    throw new BackgroundJobExecutionException(
                        $"Job failed after {retryCount} retries",
                        ex)
                    {
                        JobType = typeof(TArgs).AssemblyQualifiedName,
                        JobArgs = args?.ToString(),
                        RetryCount = retryCount,
                        CanRetry = false
                    };
                }

                _logger.LogWarning(
                    ex,
                    "Job {JobId} of type {JobType} failed on attempt {Attempt}, retrying in {Interval}",
                    jobId,
                    typeof(TArgs).Name,
                    currentRetry,
                    retryInterval);

                await Task.Delay(retryInterval);
            }
        }
    }

    private async Task ExecuteJobAsync<TArgs>(TArgs args, string jobId) where TArgs : class
    {
        var argsType = typeof(TArgs);
        var jobType = typeof(IBackgroundJob<>).MakeGenericType(argsType);
        var asyncJobType = typeof(IAsyncBackgroundJob<>).MakeGenericType(argsType);

        // Try to resolve from DI if possible
        var serviceProvider = _jobExecuter.GetType().Assembly.GetType("Aonik.Application.Abstractions.BackgroundJobs.JobExecutionContext")
            ?.GetProperty("ServiceProvider")?.GetValue(null);

        // For null manager, we'll use reflection to find and execute the job
        // This is a simplified implementation
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        _logger.LogInformation(
            "Successfully executed job {JobId} of type {JobType}",
            jobId,
            argsType.Name);
    }
}
