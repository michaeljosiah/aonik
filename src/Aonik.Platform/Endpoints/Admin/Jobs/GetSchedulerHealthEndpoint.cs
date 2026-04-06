using Aonik.Platform.Contracts.Api.Jobs;
using Aonik.Platform.Contracts.Services.Operations;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Jobs;

internal class GetSchedulerHealthEndpoint : EndpointWithoutRequest<SchedulerHealthResponse>
{
    private readonly IScheduledJobAdminService _scheduledJobAdminService;

    public GetSchedulerHealthEndpoint(IScheduledJobAdminService scheduledJobAdminService)
    {
        _scheduledJobAdminService = scheduledJobAdminService;
    }

    public override void Configure()
    {
        Get("/admin/scheduler/health");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Check scheduler health";
            s.Description = "Returns the health status of the background job scheduler including uptime and job statistics.";
            s.Response(200, "Scheduler health");
            s.Response(401, "Not authenticated");
            s.Response(404, "Scheduler status unavailable");
        });
        Options(x => x.WithTags("System Administration"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await _scheduledJobAdminService.GetSchedulerHealthAsync(ct);
        if (result is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(result, ct);
    }
}
