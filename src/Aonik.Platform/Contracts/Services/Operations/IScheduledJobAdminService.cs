using Aonik.Platform.Contracts.Api.Jobs;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Platform.Contracts.Services.Operations;

public interface IScheduledJobAdminService
{
    Task<ScheduledJobListResponse> ListScheduledJobsAsync(CancellationToken cancellationToken = default);

    Task<ScheduledJobDetailResponse?> GetJobDetailAsync(string jobName, CancellationToken cancellationToken = default);

    Task<PagedResult<ScheduledJobRunSummary>> ListJobRunsAsync(
        string jobName, int pageNumber, int pageSize, CancellationToken cancellationToken = default);

    Task<PagedResult<ScheduledJobCommandSummary>> ListJobCommandsAsync(
        string jobName, int pageNumber, int pageSize, CancellationToken cancellationToken = default);

    Task<SchedulerHealthResponse?> GetSchedulerHealthAsync(CancellationToken cancellationToken = default);

    Task<ScheduledJobActionResponse?> QueuePauseAsync(string jobName, CancellationToken cancellationToken = default);

    Task<ScheduledJobActionResponse?> QueueResumeAsync(string jobName, CancellationToken cancellationToken = default);

    Task<ScheduledJobActionResponse?> QueueTriggerAsync(string jobName, CancellationToken cancellationToken = default);

    Task<ScheduledJobConfigurationResponse?> GetJobConfigurationAsync(string jobName, CancellationToken cancellationToken = default);

    Task<ScheduledJobConfigurationResponse?> UpdateJobConfigurationAsync(string jobName, string? configurationJson, CancellationToken cancellationToken = default);
}
