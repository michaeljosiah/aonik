using Aonik.Platform.Contracts.Api.Jobs;
using Aonik.Platform.Contracts.Services.Operations;
using Aonik.SharedKernel.Abstractions;
using FastEndpoints;

namespace Aonik.Platform.Endpoints.Admin.Jobs;

internal sealed record ListScheduledJobRunsRequest
{
    public string JobName { get; init; } = string.Empty;

    [QueryParam]
    public int PageNumber { get; init; } = 1;

    [QueryParam]
    public int PageSize { get; init; } = 20;
}

internal class ListScheduledJobRunsEndpoint
    : Endpoint<ListScheduledJobRunsRequest, PagedResult<ScheduledJobRunSummary>>
{
    private readonly IScheduledJobAdminService _scheduledJobAdminService;

    public ListScheduledJobRunsEndpoint(IScheduledJobAdminService scheduledJobAdminService)
    {
        _scheduledJobAdminService = scheduledJobAdminService;
    }

    public override void Configure()
    {
        Get("/admin/jobs/scheduled/{jobName}/runs");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(ListScheduledJobRunsRequest req, CancellationToken ct)
    {
        var jobName = Route<string>("jobName")!;

        var result = await _scheduledJobAdminService.ListJobRunsAsync(
            jobName, req.PageNumber, req.PageSize, ct);

        await Send.OkAsync(result, ct);
    }
}
