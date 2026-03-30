using Aonik.Platform.Contracts.Api.Jobs;
using Aonik.Platform.Contracts.Services.Operations;
using FastEndpoints;

namespace Aonik.Platform.Endpoints.Admin.Jobs;

/// <summary>
/// Requests an immediate trigger of a scheduled job by setting a command
/// on the Job entity that the Worker service reads and acts on.
/// </summary>
internal class TriggerScheduledJobEndpoint : EndpointWithoutRequest<ScheduledJobActionResponse>
{
    private readonly IScheduledJobAdminService _scheduledJobAdminService;

    public TriggerScheduledJobEndpoint(IScheduledJobAdminService scheduledJobAdminService)
    {
        _scheduledJobAdminService = scheduledJobAdminService;
    }

    public override void Configure()
    {
        Post("/admin/jobs/scheduled/{jobName}/trigger");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var jobName = Route<string>("jobName")!;

        var result = await _scheduledJobAdminService.QueueTriggerAsync(jobName, ct);
        if (result is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(result, ct);
    }
}
