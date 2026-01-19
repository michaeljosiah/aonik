namespace Aonik.Application.Abstractions.BackgroundJobs;

/// <summary>
/// Defines the interface for a synchronous background job.
/// </summary>
/// <typeparam name="TArgs">The type of arguments the job accepts.</typeparam>
public interface IBackgroundJob<in TArgs>
{
    /// <summary>
    /// Executes the job with the specified arguments.
    /// </summary>
    /// <param name="args">The job arguments.</param>
    void Execute(TArgs args);
}
