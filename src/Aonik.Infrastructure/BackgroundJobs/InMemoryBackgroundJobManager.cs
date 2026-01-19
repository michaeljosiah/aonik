using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aonik.Application.Abstractions.BackgroundJobs;

namespace Aonik.Infrastructure.BackgroundJobs;

/// <summary>
/// In-memory background job manager that executes jobs synchronously in the current thread.
/// Useful for testing or simple scenarios where a full job scheduler is not needed.
/// </summary>
public class InMemoryBackgroundJobManager : IBackgroundJobManager
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly AonikBackgroundJobOptions _options;
    private readonly ILogger<InMemoryBackgroundJobManager> _logger;
    private static readonly ConcurrentQueue<(Action Action, string JobId, Type JobType)> _jobQueue = new();
    private static readonly CancellationTokenSource _cts = new();
    private static Task? _processingTask;

    /// <summary>
    /// Creates a new instance of <see cref="InMemoryBackgroundJobManager"/>
    /// </summary>
    public InMemoryBackgroundJobManager(
        IServiceScopeFactory serviceScopeFactory,
        AonikBackgroundJobOptions options,
        ILogger<InMemoryBackgroundJobManager>? logger = null)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _options = options;
        _logger = logger ?? NullLogger<InMemoryBackgroundJobManager>.Instance;

        StartProcessing();
    }

    /// <inheritdoc />
    public Task<string> EnqueueAsync<TArgs>(
        TArgs args,
        BackgroundJobPriority priority = BackgroundJobPriority.Normal,
        TimeSpan? delay = null) where TArgs : class
    {
        return EnqueueWithRetryAsync(
            args,
            _options.DefaultMaxRetryCount,
            _options.DefaultRetryInterval,
            priority,
            delay);
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
        var argsType = typeof(TArgs);

        _logger.LogInformation(
            "Enqueued in-memory job {JobId} of type {JobType} with delay {Delay}",
            jobId,
            argsType.Name,
            delay ?? TimeSpan.Zero);

        if (delay.HasValue && delay.Value > TimeSpan.Zero)
        {
            await Task.Delay(delay.Value);
        }

        var jobConfig = _options.GetJob<TArgs>();

        var jobAction = new Action(() =>
        {
            ExecuteJobAsync(args, jobId, jobConfig.JobType, retryCount, retryInterval).GetAwaiter().GetResult();
        });

        _jobQueue.Enqueue((jobAction, jobId, argsType));

        return jobId;
    }

    private async Task ExecuteJobAsync<TArgs>(
        TArgs args,
        string jobId,
        Type jobType,
        int maxRetries,
        TimeSpan retryInterval) where TArgs : class
    {
        int currentRetry = 0;

        while (true)
        {
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var serviceProvider = scope.ServiceProvider;

                var job = serviceProvider.GetService(jobType);
                if (job == null)
                {
                    throw new BackgroundJobExecutionException(
                        $"Failed to resolve job type: {jobType.FullName}")
                    {
                        JobType = jobType.AssemblyQualifiedName,
                        JobArgs = JsonSerializer.Serialize(args),
                        CanRetry = false
                    };
                }

                var executeMethod = jobType.GetMethod(nameof(IBackgroundJob<object>.Execute));
                var asyncExecuteMethod = jobType.GetMethod(
                    nameof(IAsyncBackgroundJob<object>.ExecuteAsync),
                    BindingFlags.Public | BindingFlags.Instance);

                if (asyncExecuteMethod != null)
                {
                    var task = (Task)asyncExecuteMethod.Invoke(job, new object[] { args, CancellationToken.None })!;
                    await task.ConfigureAwait(false);
                }
                else if (executeMethod != null)
                {
                    executeMethod.Invoke(job, new object[] { args });
                }

                _logger.LogInformation("Successfully executed in-memory job {JobId}", jobId);
                return;
            }
            catch (Exception ex)
            {
                currentRetry++;

                if (currentRetry > maxRetries)
                {
                    _logger.LogError(
                        ex,
                        "In-memory job {JobId} failed after {MaxRetries} retries",
                        jobId,
                        maxRetries);

                    throw new BackgroundJobExecutionException(
                        $"Job failed after {maxRetries} retries: {ex.Message}",
                        ex)
                    {
                        JobType = jobType.AssemblyQualifiedName,
                        JobArgs = JsonSerializer.Serialize(args),
                        RetryCount = maxRetries,
                        CanRetry = false
                    };
                }

                _logger.LogWarning(
                    ex,
                    "In-memory job {JobId} failed on attempt {Attempt}, retrying in {Interval}",
                    jobId,
                    currentRetry,
                    retryInterval);

                await Task.Delay(retryInterval);
            }
        }
    }

    private static void StartProcessing()
    {
        if (_processingTask != null && !_processingTask.IsCompleted)
        {
            return;
        }

        _processingTask = Task.Run(() =>
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                if (_jobQueue.TryDequeue(out var job))
                {
                    try
                    {
                        job.Action();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Job {job.JobId} failed: {ex.Message}");
                    }
                }
                else
                {
                    Thread.Sleep(100); // Wait for new jobs
                }
            }
        });
    }

    /// <summary>
    /// Stops the in-memory job processor.
    /// </summary>
    public static void StopProcessing()
    {
        _cts.Cancel();
    }
}
