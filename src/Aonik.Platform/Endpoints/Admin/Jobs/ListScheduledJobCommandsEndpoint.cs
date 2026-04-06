using Aonik.Platform.Contracts.Api.Jobs;
using Aonik.Platform.Contracts.Services.Operations;
using Aonik.SharedKernel.Abstractions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Jobs;

internal sealed record ListScheduledJobCommandsRequest
{
    public string JobName { get; init; } = string.Empty;

    [QueryParam]
    public int PageNumber { get; init; } = 1;

    [QueryParam]
    public int PageSize { get; init; } = 20;
}

internal class ListScheduledJobCommandsEndpoint
    : Endpoint<ListScheduledJobCommandsRequest, PagedResult<ScheduledJobCommandSummary>>
{
    private readonly IScheduledJobAdminService _scheduledJobAdminService;

    public ListScheduledJobCommandsEndpoint(IScheduledJobAdminService scheduledJobAdminService)
    {
        _scheduledJobAdminService = scheduledJobAdminService;
    }

    public override void Configure()
    {
        Get("/admin/jobs/scheduled/{jobName}/commands");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "List scheduled job commands";
            s.Description = "Returns a paginated list of admin commands (pause, resume, trigger) issued for the specified job.";
            s.Response(200, "Paged command list");
            s.Response(401, "Not authenticated");
            s.Response(404, "Job not found");
        });
        Options(x => x.WithTags("System Administration"));
    }

    public override async Task HandleAsync(ListScheduledJobCommandsRequest req, CancellationToken ct)
    {
        var jobName = Route<string>("jobName")!;

        var result = await _scheduledJobAdminService.ListJobCommandsAsync(
            jobName, req.PageNumber, req.PageSize, ct);

        await Send.OkAsync(result, ct);
    }
}
