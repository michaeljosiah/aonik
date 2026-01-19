using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Impl;
using Quartz.Spi;
using Aonik.Application.Abstractions.BackgroundJobs;
using Aonik.Infrastructure.BackgroundJobs.Quartz;

namespace Aonik.Worker;

/// <summary>
/// Background worker that hosts the Quartz scheduler for background jobs.
/// This is the entry point for all background job processing.
/// </summary>
public class QuartzHostedService : BackgroundService
{
    private readonly ILogger<QuartzHostedService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly QuartzBackgroundJobOptions _options;

    public QuartzHostedService(
        ILogger<QuartzHostedService> logger,
        IServiceProvider serviceProvider,
        QuartzBackgroundJobOptions options)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _options = options;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Quartz background job service starting...");

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var scheduler = scope.ServiceProvider.GetRequiredService<IScheduler>();

            // Configure scheduler
            scheduler.JobFactory = new AonikJobFactory(scope.ServiceProvider);

            if (_options.AutoStartScheduler)
            {
                await scheduler.Start(stoppingToken);
                _logger.LogInformation("Quartz scheduler started successfully");
            }

            // Keep the service running
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    var jobs = await scheduler.GetJobKeys(Quartz.Impl.Matchers.GroupMatcher<Quartz.JobKey>.AnyGroup());
                    _logger.LogDebug("Quartz scheduler running with {JobCount} jobs", jobs.Count);
                }
            }

            _logger.LogInformation("Quartz background job service stopping...");
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
            _logger.LogInformation("Quartz background job service cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Quartz background job service");
            throw;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Quartz background job service stopping...");

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var scheduler = scope.ServiceProvider.GetRequiredService<IScheduler>();

            await scheduler.Shutdown(waitForJobsToComplete: true, cancellationToken);

            _logger.LogInformation("Quartz scheduler shutdown complete");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error shutting down Quartz scheduler");
        }

        await base.StopAsync(cancellationToken);
    }
}

/// <summary>
/// Custom Quartz job factory that creates jobs using AONIK's DI container.
/// </summary>
public class AonikJobFactory : IJobFactory
{
    private readonly IServiceProvider _serviceProvider;

    public AonikJobFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IJob NewJob(TriggerFiredBundle bundle, IScheduler scheduler)
    {
        // Get the job type from the bundle
        var jobDetail = bundle.JobDetail;
        var jobType = jobDetail.JobType;

        // Get the generic argument type (TArgs)
        var argsType = jobType.BaseType?.GetGenericArguments()[0];

        if (argsType == null)
        {
            throw new InvalidOperationException($"Cannot determine job args type for {jobType.Name}");
        }

        // Create the adapter type
        var adapterType = typeof(QuartzJobExecutionAdapter<>).MakeGenericType(argsType);

        // Resolve from DI
        var job = _serviceProvider.GetRequiredService(adapterType);

        return (IJob)job;
    }

    public void ReturnJob(IJob job)
    {
        // Jobs are resolved from DI scope, no explicit return needed
        // The scope will be disposed automatically
    }
}
