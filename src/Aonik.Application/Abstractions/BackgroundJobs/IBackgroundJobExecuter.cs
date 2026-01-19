using System.Threading.Tasks;

namespace Aonik.Application.Abstractions.BackgroundJobs;

/// <summary>
/// Defines the interface for executing background jobs.
/// </summary>
public interface IBackgroundJobExecuter
{
    /// <summary>
    /// Executes the specified job in the given context.
    /// </summary>
    /// <param name="context">The job execution context.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ExecuteAsync(JobExecutionContext context);
}
