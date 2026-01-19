using System;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Quartz.Impl;
using Quartz.Spi;
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
    /// Adds Quartz-based background job services.
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

        // Register job executor
        services.AddScoped<IBackgroundJobExecuter, BackgroundJobExecuter>();

        // Register JSON serializer
        services.AddSingleton<IJsonSerializer, SystemTextJsonSerializer>();

        // Register Quartz scheduler
        services.AddSingleton<IScheduler>(sp =>
        {
            var schedulerFactory = new StdSchedulerFactory();
            return schedulerFactory.GetScheduler().GetAwaiter().GetResult();
        });

        // Register Quartz job adapter
        services.AddScoped(typeof(QuartzJobExecutionAdapter<>));

        // Register background job manager
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
