using Aonik.Platform.Contracts.Api.Jobs;
using Aonik.Platform.Contracts.Services.Operations;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Jobs;

internal class GetScheduledJobDetailEndpoint : EndpointWithoutRequest<ScheduledJobDetailResponse>
{
    private readonly IScheduledJobAdminService _scheduledJobAdminService;

    public GetScheduledJobDetailEndpoint(IScheduledJobAdminService scheduledJobAdminService)
    {
        _scheduledJobAdminService = scheduledJobAdminService;
    }

    public override void Configure()
    {
        Get("/admin/jobs/scheduled/{jobName}");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Get scheduled job details";
            s.Description = "Retrieves the full detail of a scheduled job including its schedule, status, and last run information.";
            s.Response(200, "Job details");
            s.Response(401, "Not authenticated");
            s.Response(404, "Job not found");
        });
        Options(x => x.WithTags("System Administration"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var jobName = Route<string>("jobName")!;

        var result = await _scheduledJobAdminService.GetJobDetailAsync(jobName, ct);
        if (result is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(result, ct);
    }
}
