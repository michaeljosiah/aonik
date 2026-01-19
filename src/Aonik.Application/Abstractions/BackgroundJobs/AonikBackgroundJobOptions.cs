using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Aonik.Application.Abstractions.BackgroundJobs;

/// <summary>
/// Provides configuration options for background jobs.
/// </summary>
public class AonikBackgroundJobOptions
{
    private readonly Dictionary<Type, BackgroundJobConfiguration> _jobConfigurationsByArgsType;
    private readonly Dictionary<string, BackgroundJobConfiguration> _jobConfigurationsByName;

    /// <summary>
    /// Gets or sets whether job execution is enabled.
    /// Default: true.
    /// </summary>
    public bool IsJobExecutionEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the default maximum retry count for jobs.
    /// Default: 3.
    /// </summary>
    public int DefaultMaxRetryCount { get; set; } = 3;

    /// <summary>
    /// Gets or sets the default retry interval.
    /// Default: 1 minute.
    /// </summary>
    public TimeSpan DefaultRetryInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets or sets the function to determine a job's name from its type.
    /// </summary>
    public Func<Type, string> GetBackgroundJobName { get; set; } = type => type.Name;

    /// <summary>
    /// Gets the list of registered job types.
    /// </summary>
    public IReadOnlyList<Type> RegisteredJobTypes => _jobConfigurationsByArgsType.Values
        .Select(c => c.JobType)
        .ToImmutableList();

    /// <summary>
    /// Creates a new instance of <see cref="AonikBackgroundJobOptions"/>
    /// </summary>
    public AonikBackgroundJobOptions()
    {
        _jobConfigurationsByArgsType = new Dictionary<Type, BackgroundJobConfiguration>();
        _jobConfigurationsByName = new Dictionary<string, BackgroundJobConfiguration>();
    }

    /// <summary>
    /// Gets the job configuration for the specified arguments type.
    /// </summary>
    /// <typeparam name="TArgs">The arguments type.</typeparam>
    /// <returns>The job configuration.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no job is registered for the type.</exception>
    public BackgroundJobConfiguration GetJob<TArgs>()
    {
        return GetJob(typeof(TArgs));
    }

    /// <summary>
    /// Gets the job configuration for the specified arguments type.
    /// </summary>
    /// <param name="argsType">The arguments type.</param>
    /// <returns>The job configuration.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no job is registered for the type.</exception>
    public BackgroundJobConfiguration GetJob(Type argsType)
    {
        if (_jobConfigurationsByArgsType.TryGetValue(argsType, out var config))
        {
            return config;
        }

        throw new InvalidOperationException(
            $"No background job is registered for the arguments type: {argsType.FullName}");
    }

    /// <summary>
    /// Gets the job configuration by job name.
    /// </summary>
    /// <param name="jobName">The job name.</param>
    /// <returns>The job configuration.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no job is registered with the name.</exception>
    public BackgroundJobConfiguration GetJob(string jobName)
    {
        if (_jobConfigurationsByName.TryGetValue(jobName, out var config))
        {
            return config;
        }

        throw new InvalidOperationException(
            $"No background job is registered with the name: {jobName}");
    }

    /// <summary>
    /// Gets all registered job configurations.
    /// </summary>
    /// <returns>Read-only list of job configurations.</returns>
    public IReadOnlyList<BackgroundJobConfiguration> GetJobs()
    {
        return _jobConfigurationsByArgsType.Values.ToImmutableList();
    }

    /// <summary>
    /// Registers a job type automatically.
    /// </summary>
    /// <typeparam name="TJob">The job type to register.</typeparam>
    public void AddJob<TJob>() where TJob : class
    {
        AddJob(typeof(TJob));
    }

    /// <summary>
    /// Registers a job type.
    /// </summary>
    /// <param name="jobType">The job type to register.</param>
    public void AddJob(Type jobType)
    {
        var config = new BackgroundJobConfiguration(jobType, GetBackgroundJobName(jobType));
        AddJob(config);
    }

    /// <summary>
    /// Registers a job with explicit configuration.
    /// </summary>
    /// <param name="configuration">The job configuration.</param>
    public void AddJob(BackgroundJobConfiguration configuration)
    {
        _jobConfigurationsByArgsType[configuration.ArgsType] = configuration;
        _jobConfigurationsByName[configuration.JobName] = configuration;
    }

    /// <summary>
    /// Checks if a job is registered for the specified arguments type.
    /// </summary>
    /// <typeparam name="TArgs">The arguments type.</typeparam>
    /// <returns>True if registered; otherwise, false.</returns>
    public bool IsJobRegistered<TArgs>()
    {
        return _jobConfigurationsByArgsType.ContainsKey(typeof(TArgs));
    }

    /// <summary>
    /// Checks if a job is registered for the specified arguments type.
    /// </summary>
    /// <param name="argsType">The arguments type.</param>
    /// <returns>True if registered; otherwise, false.</returns>
    public bool IsJobRegistered(Type argsType)
    {
        return _jobConfigurationsByArgsType.ContainsKey(argsType);
    }
}
