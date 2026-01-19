using System.Threading.Tasks;

namespace Aonik.Application.Abstractions.BackgroundJobs;

/// <summary>
/// Defines the interface for an asynchronous background job.
/// </summary>
/// <typeparam name="TArgs">The type of arguments the job accepts.</typeparam>
public interface IAsyncBackgroundJob<in TArgs>
{
    /// <summary>
    /// Executes the job asynchronously with the specified arguments.
    /// </summary>
    /// <param name="args">The job arguments.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ExecuteAsync(TArgs args, CancellationToken cancellationToken = default);
}
