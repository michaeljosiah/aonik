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
    /// Adds the core background job services shared by all hosts.
    /// </summary>
    public static IServiceCollection AddAonikBackgroundJobCoreServices(
        this IServiceCollection services,
        Action<AonikBackgroundJobOptions>? configureOptions = null)
    {
        services.Configure<AonikBackgroundJobOptions>(options =>
        {
            options.IsJobExecutionEnabled = true;
            configureOptions?.Invoke(options);
        });

        services.AddScoped<IBackgroundJobExecuter, BackgroundJobExecuter>();

        return services;
    }

    /// <summary>
    /// Adds the Quartz-backed background job runtime. Only execution hosts such
    /// as the Worker should register this runtime scheduler integration.
    /// </summary>
    public static IServiceCollection AddQuartzBackgroundJobRuntime(
        this IServiceCollection services,
        Action<QuartzBackgroundJobOptions>? configureOptions = null)
    {
        var quartzOptions = new QuartzBackgroundJobOptions();
        configureOptions?.Invoke(quartzOptions);
        services.AddSingleton(quartzOptions);

        services.AddQuartz();

        // Register Quartz job adapter (for on-demand enqueued jobs)
        services.AddScoped(typeof(QuartzJobExecutionAdapter<>));

        // Register background job manager (for on-demand enqueued jobs)
        services.AddScoped<IBackgroundJobManager, QuartzBackgroundJobManager>();

        return services;
    }

    /// <summary>
    /// Adds the shared core services and Quartz runtime in one call.
    /// </summary>
    public static IServiceCollection AddAonikBackgroundJobs(
        this IServiceCollection services,
        Action<QuartzBackgroundJobOptions>? configureOptions = null)
    {
        services.AddAonikBackgroundJobCoreServices();
        services.AddQuartzBackgroundJobRuntime(configureOptions);
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
