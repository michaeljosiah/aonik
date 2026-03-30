using Aonik.Platform.Contracts.Api.Jobs;
using Aonik.Platform.Contracts.Services.Operations;
using FastEndpoints;

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
