using System;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Aonik.Application.Abstractions;
using Aonik.Application.Abstractions.BackgroundJobs;
using Aonik.Infrastructure.BackgroundJobs.Quartz;

namespace Aonik.Infrastructure.BackgroundJobs;

/// <summary>
/// Extension methods for registering background job services.
/// </summary>
public static class BackgroundJobsExtensions
{
    /// <summary>
    /// Adds background job services (executor, serializer, job manager) and a
    /// baseline Quartz scheduler. Host projects (e.g. Worker) can call
    /// <c>AddQuartz()</c> again to add cron jobs, persistent store, etc.
    /// — Quartz merges multiple <c>AddQuartz</c> calls.
    /// </summary>
    public static IServiceCollection AddAonikBackgroundJobs(
        this IServiceCollection services,
        Action<QuartzBackgroundJobOptions>? configureOptions = null)
    {
        // Register options
        services.Configure<AonikBackgroundJobOptions>(options =>
        {
            options.IsJobExecutionEnabled = true;
        });

        var quartzOptions = new QuartzBackgroundJobOptions();
        configureOptions?.Invoke(quartzOptions);
        services.AddSingleton(quartzOptions);

        // Register a baseline Quartz scheduler (IScheduler) so that
        // QuartzBackgroundJobManager can resolve it in any host project.
        // Host projects that need cron jobs or a persistent store call
        // AddQuartz() again — Quartz merges the configurations.
        services.AddQuartz();

        // Register job executor
        services.AddScoped<IBackgroundJobExecuter, BackgroundJobExecuter>();

        // Register JSON serializer
        services.AddSingleton<IJsonSerializer, SystemTextJsonSerializer>();

        // Register Quartz job adapter (for on-demand enqueued jobs)
        services.AddScoped(typeof(QuartzJobExecutionAdapter<>));

        // Register background job manager (for on-demand enqueued jobs)
        services.AddScoped<IBackgroundJobManager, QuartzBackgroundJobManager>();

        return services;
    }

    /// <summary>
    /// Adds in-memory background job services (for testing or when Quartz is not available).
    /// </summary>
    public static IServiceCollection AddInMemoryBackgroundJobs(
        this IServiceCollection services,
        Action<AonikBackgroundJobOptions>? configureOptions = null)
    {
        services.Configure<AonikBackgroundJobOptions>(options =>
        {
            options.IsJobExecutionEnabled = true;
            configureOptions?.Invoke(options);
        });

        // Register JSON serializer
        services.AddSingleton<IJsonSerializer, SystemTextJsonSerializer>();

        // Register job executor
        services.AddScoped<IBackgroundJobExecuter, BackgroundJobExecuter>();

        // Use null manager for in-memory execution
        services.AddScoped<IBackgroundJobManager, InMemoryBackgroundJobManager>();

        return services;
    }
}
