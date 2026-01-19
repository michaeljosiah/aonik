using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Aonik.Application.Abstractions.BackgroundJobs;
using Aonik.Application.Abstractions.Multitenancy;
using Aonik.Application.Abstractions.Persistence;

namespace Aonik.Infrastructure.BackgroundJobs;

/// <summary>
/// Default implementation of <see cref="IBackgroundJobExecuter"/> that executes background jobs.
/// </summary>
public class BackgroundJobExecuter : IBackgroundJobExecuter
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly AonikBackgroundJobOptions _options;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<BackgroundJobExecuter> _logger;

    /// <summary>
    /// Creates a new instance of <see cref="BackgroundJobExecuter"/>
    /// </summary>
    public BackgroundJobExecuter(
        IServiceScopeFactory serviceScopeFactory,
        IOptions<AonikBackgroundJobOptions> options,
        ITenantProvider tenantProvider,
        ILogger<BackgroundJobExecuter>? logger = null)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _options = options.Value;
        _tenantProvider = tenantProvider;
        _logger = logger ?? NullLogger<BackgroundJobExecuter>.Instance;
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(JobExecutionContext context)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var serviceProvider = scope.ServiceProvider;

        // Resolve the job instance from DI
        var job = serviceProvider.GetService(context.JobType);
        if (job == null)
        {
            throw new BackgroundJobExecutionException(
                $"Failed to resolve background job type: {context.JobType.FullName}")
            {
                JobType = context.JobType.AssemblyQualifiedName,
                JobArgs = context.JobArgs?.ToString(),
                CanRetry = false
            };
        }

        // Find the Execute or ExecuteAsync method
        var executeMethod = context.JobType.GetMethod(nameof(IBackgroundJob<object>.Execute));
        var asyncExecuteMethod = context.JobType.GetMethod(
            nameof(IAsyncBackgroundJob<object>.ExecuteAsync),
            BindingFlags.Public | BindingFlags.Instance);

        if (executeMethod == null && asyncExecuteMethod == null)
        {
            throw new BackgroundJobExecutionException(
                $"Background job type {context.JobType.Name} does not implement IBackgroundJob<TArgs> or IAsyncBackgroundJob<TArgs>")
            {
                JobType = context.JobType.AssemblyQualifiedName,
                JobArgs = context.JobArgs?.ToString(),
                CanRetry = false
            };
        }

        try
        {
            // Set tenant context for the job (using ITenantProvider pattern)
            var tenantId = GetJobArgsTenantId(context.JobArgs);

            if (asyncExecuteMethod != null)
            {
                // Execute async job
                var task = (Task)asyncExecuteMethod.Invoke(job, new[] { context.JobArgs, context.CancellationToken })!;
                await task.ConfigureAwait(false);
            }
            else if (executeMethod != null)
            {
                // Execute sync job
                executeMethod.Invoke(job, new[] { context.JobArgs });
            }

            _logger.LogInformation(
                "Successfully executed background job {JobType}",
                context.JobType.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Background job {JobType} execution failed",
                context.JobType.Name);

            var isTransient = IsTransientException(ex);

            throw new BackgroundJobExecutionException(
                $"Background job execution failed: {ex.Message}",
                ex)
            {
                JobType = context.JobType.AssemblyQualifiedName,
                JobArgs = context.JobArgs?.ToString(),
                CanRetry = isTransient
            };
        }
    }

    private Guid? GetJobArgsTenantId(object jobArgs)
    {
        if (jobArgs == null)
        {
            return _tenantProvider.TryGetCurrentTenantId(out var id) ? id : null;
        }

        // Check if job args implements ITenantScoped
        var tenantScopedType = jobArgs.GetType().GetInterface("Aonik.Application.Abstractions.Multitenancy.ITenantScoped");
        if (tenantScopedType != null)
        {
            var tenantIdProperty = tenantScopedType.GetProperty("TenantId");
            if (tenantIdProperty != null)
            {
                return tenantIdProperty.GetValue(jobArgs) as Guid?;
            }
        }

        return _tenantProvider.TryGetCurrentTenantId(out var currentId) ? currentId : null;
    }

    private static bool IsTransientException(Exception ex)
    {
        // Common transient exceptions that can be retried
        return ex is TimeoutException ||
               ex is InvalidOperationException ||
               (ex.InnerException != null && IsTransientException(ex.InnerException));
    }
}
