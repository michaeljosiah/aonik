using Aonik.Platform.Contracts.Api.Jobs;
using Aonik.Platform.Contracts.Services.Operations;
using FastEndpoints;

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
