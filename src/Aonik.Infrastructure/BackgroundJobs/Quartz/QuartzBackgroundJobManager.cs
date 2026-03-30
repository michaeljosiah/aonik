using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Quartz;
using Aonik.Application.Abstractions;
using Aonik.Application.Abstractions.BackgroundJobs;

namespace Aonik.Infrastructure.BackgroundJobs.Quartz;

/// <summary>
/// Quartz.NET implementation of <see cref="IBackgroundJobManager"/>.
/// </summary>
public class QuartzBackgroundJobManager : IBackgroundJobManager
{
    private const string JobDataPrefix = "Aonik";
    public const string RetryIndexKey = "RetryIndex";
    public const string RetryCountKey = "RetryCount";
    public const string RetryIntervalKey = "RetryInterval";
    public const string JobArgsKey = "JobArgs";

    private readonly ISchedulerFactory _schedulerFactory;
    private readonly QuartzBackgroundJobOptions _options;
    private readonly IJsonSerializer _jsonSerializer;
    private readonly ILogger<QuartzBackgroundJobManager> _logger;

    /// <summary>
    /// Creates a new instance of <see cref="QuartzBackgroundJobManager"/>
    /// </summary>
    public QuartzBackgroundJobManager(
        ISchedulerFactory schedulerFactory,
        QuartzBackgroundJobOptions options,
        IJsonSerializer jsonSerializer,
        ILogger<QuartzBackgroundJobManager>? logger = null)
    {
        _schedulerFactory = schedulerFactory ?? throw new ArgumentNullException(nameof(schedulerFactory));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _jsonSerializer = jsonSerializer ?? throw new ArgumentNullException(nameof(jsonSerializer));
        _logger = logger ?? NullLogger<QuartzBackgroundJobManager>.Instance;
    }

    /// <inheritdoc />
    public async Task<string> EnqueueAsync<TArgs>(
        TArgs args,
        BackgroundJobPriority priority = BackgroundJobPriority.Normal,
        TimeSpan? delay = null) where TArgs : class
    {
        return await EnqueueWithRetryAsync(
            args,
            _options.DefaultRetryCount,
            TimeSpan.FromMilliseconds(_options.DefaultRetryIntervalMilliseconds),
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

        var jobDataMap = new JobDataMap
        {
            { JobArgsKey, _jsonSerializer.Serialize(args) },
            { JobDataPrefix + RetryCountKey, retryCount.ToString() },
            { JobDataPrefix + RetryIntervalKey, retryInterval.TotalMilliseconds.ToString() },
            { JobDataPrefix + RetryIndexKey, "0" }
        };

        var jobDetail = JobBuilder
            .Create<QuartzJobExecutionAdapter<TArgs>>()
            .WithIdentity(new JobKey(jobId, "AonikBackgroundJobs"))
            .RequestRecovery() // Recover after scheduler restart
            .SetJobData(jobDataMap)
            .Build();

        var triggerBuilder = TriggerBuilder
            .Create()
            .WithIdentity(new TriggerKey($"{jobId}_Trigger", "AonikBackgroundJobs"))
            .WithPriority((int)priority);

        if (delay.HasValue && delay.Value > TimeSpan.Zero)
        {
            triggerBuilder.StartAt(DateTimeOffset.UtcNow.Add(delay.Value));
        }
        else
        {
            triggerBuilder.StartNow();
        }

        var trigger = triggerBuilder.Build();

        var scheduler = await _schedulerFactory.GetScheduler();
        await scheduler.ScheduleJob(jobDetail, trigger);

        _logger.LogInformation(
            "Enqueued background job {JobId} of type {JobType} with priority {Priority} and delay {Delay}",
            jobId,
            typeof(TArgs).Name,
            priority,
            delay ?? TimeSpan.Zero);

        return jobId;
    }
}
