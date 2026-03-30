using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Quartz;
using Aonik.Application.Abstractions;
using Aonik.Application.Abstractions.BackgroundJobs;

namespace Aonik.Infrastructure.BackgroundJobs.Quartz;

/// <summary>
/// Quartz job adapter that executes AONIK background jobs.
/// This class implements Quartz's IJob interface and adapts it to AONIK's job execution pattern.
/// </summary>
/// <typeparam name="TArgs">The type of job arguments.</typeparam>
public class QuartzJobExecutionAdapter<TArgs> : IJob where TArgs : class
{
    private const string JobDataPrefix = "Aonik";
    public const string RetryIndexKey = "RetryIndex";
    public const string RetryCountKey = "RetryCount";
    public const string RetryIntervalKey = "RetryInterval";
    public const string JobArgsKey = "JobArgs";

    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IBackgroundJobExecuter _jobExecuter;
    private readonly AonikBackgroundJobOptions _backgroundJobOptions;
    private readonly QuartzBackgroundJobOptions _quartzOptions;
    private readonly IJsonSerializer _jsonSerializer;
    private readonly ILogger<QuartzJobExecutionAdapter<TArgs>> _logger;

    /// <summary>
    /// Creates a new instance of <see cref="QuartzJobExecutionAdapter{TArgs}"/>
    /// </summary>
    public QuartzJobExecutionAdapter(
        IServiceScopeFactory serviceScopeFactory,
        IBackgroundJobExecuter jobExecuter,
        AonikBackgroundJobOptions backgroundJobOptions,
        QuartzBackgroundJobOptions quartzOptions,
        IJsonSerializer jsonSerializer,
        ILogger<QuartzJobExecutionAdapter<TArgs>>? logger = null)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _jobExecuter = jobExecuter;
        _backgroundJobOptions = backgroundJobOptions;
        _quartzOptions = quartzOptions;
        _jsonSerializer = jsonSerializer;
        _logger = logger ?? NullLogger<QuartzJobExecutionAdapter<TArgs>>.Instance;
    }

    /// <inheritdoc />
    public async Task Execute(IJobExecutionContext context)
    {
        var jobKey = context.JobDetail.Key;
        _logger.LogInformation("Executing job {JobKey}", jobKey);

        try
        {
            // Deserialize job arguments
            var argsJson = context.JobDetail.JobDataMap.GetString(JobArgsKey);
            if (string.IsNullOrEmpty(argsJson))
            {
                _logger.LogError("Job {JobKey} has no arguments", jobKey);
                throw new BackgroundJobExecutionException("Job arguments are missing")
                {
                    JobType = typeof(TArgs).AssemblyQualifiedName,
                    CanRetry = false
                };
            }

            var args = _jsonSerializer.Deserialize<TArgs>(argsJson);
            if (args == null)
            {
                _logger.LogError("Failed to deserialize job arguments for {JobKey}", jobKey);
                throw new BackgroundJobExecutionException("Failed to deserialize job arguments")
                {
                    JobType = typeof(TArgs).AssemblyQualifiedName,
                    CanRetry = false
                };
            }

            // Get job configuration
            var jobConfig = _backgroundJobOptions.GetJob<TArgs>();

            // Create job execution context
            var jobContext = new JobExecutionContext(
                _serviceScopeFactory.CreateScope().ServiceProvider,
                jobConfig.JobType,
                args,
                context.CancellationToken);

            // Execute the job
            await _jobExecuter.ExecuteAsync(jobContext);

            _logger.LogInformation("Successfully executed job {JobKey}", jobKey);
        }
        catch (Exception ex)
        {
            var jobExecutionException = ex as BackgroundJobExecutionException 
                                        ?? new BackgroundJobExecutionException(
                                            $"Job execution failed: {ex.Message}", ex)
                                        {
                                            JobType = typeof(TArgs).AssemblyQualifiedName,
                                            CanRetry = IsTransientException(ex)
                                        };

            // Handle retry logic
            var retryIndexString = context.JobDetail.JobDataMap.GetString(JobDataPrefix + RetryIndexKey);
            var retryIndex = retryIndexString != null ? int.Parse(retryIndexString) : 0;
            var maxRetryCountString = context.JobDetail.JobDataMap.GetString(JobDataPrefix + RetryCountKey);
            var maxRetryCount = maxRetryCountString != null ? int.Parse(maxRetryCountString) : _quartzOptions.DefaultRetryCount;
            var retryIntervalMsString = context.JobDetail.JobDataMap.GetString(JobDataPrefix + RetryIntervalKey);
            var retryIntervalMs = retryIntervalMsString != null ? int.Parse(retryIntervalMsString) : _quartzOptions.DefaultRetryIntervalMilliseconds;

            retryIndex++;
            context.JobDetail.JobDataMap.Put(JobDataPrefix + RetryIndexKey, retryIndex.ToString());

            _logger.LogWarning(
                ex,
                "Job {JobKey} failed on attempt {Attempt} of {MaxRetry}",
                jobKey,
                retryIndex,
                maxRetryCount);

            if (retryIndex <= maxRetryCount && jobExecutionException.CanRetry)
            {
                // Schedule retry
                var triggerBuilder = TriggerBuilder
                    .Create()
                    .WithIdentity($"{jobKey.Name}_Retry_{retryIndex}", "AonikBackgroundJobs")
                    .StartAt(DateTimeOffset.UtcNow.Add(TimeSpan.FromMilliseconds(retryIntervalMs)))
                    .WithPriority(context.Trigger.Priority);

                var schedulerFactory = _serviceScopeFactory.CreateScope().ServiceProvider.GetRequiredService<ISchedulerFactory>();
                var scheduler = await schedulerFactory.GetScheduler();
                await scheduler.RescheduleJob(context.Trigger.Key, triggerBuilder.Build());

                _logger.LogInformation(
                    "Scheduled retry {RetryIndex} for job {JobKey} in {Interval}ms",
                    retryIndex,
                    jobKey,
                    retryIntervalMs);

                // Don't rethrow - the retry will handle it
                return;
            }

            _logger.LogError(
                ex,
                "Job {JobKey} failed permanently after {Attempt} attempts",
                jobKey,
                retryIndex);

            throw new JobExecutionException(
                $"Background job failed: {ex.Message}",
                ex)
            {
                RefireImmediately = false // Don't refire immediately
            };
        }
    }

    private static bool IsTransientException(Exception ex)
    {
        // Network, timeout, and transient database errors can be retried
        return ex is TimeoutException ||
               ex is InvalidOperationException ||
               ex.GetType().Name.Contains("TransientFault") ||
               (ex.InnerException != null && IsTransientException(ex.InnerException));
    }
}
