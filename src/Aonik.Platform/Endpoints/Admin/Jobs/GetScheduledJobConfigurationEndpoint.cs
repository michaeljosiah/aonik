using Aonik.Platform.Contracts.Api.Jobs;
using Aonik.Platform.Contracts.Services.Operations;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Jobs;

/// <summary>
/// Returns the runtime configuration for a scheduled job.
/// </summary>
internal class GetScheduledJobConfigurationEndpoint : EndpointWithoutRequest<ScheduledJobConfigurationResponse>
{
    private readonly IScheduledJobAdminService _scheduledJobAdminService;

    public GetScheduledJobConfigurationEndpoint(IScheduledJobAdminService scheduledJobAdminService)
    {
        _scheduledJobAdminService = scheduledJobAdminService;
    }

    public override void Configure()
    {
        Get("/admin/jobs/scheduled/{jobName}/configuration");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Get scheduled job configuration";
            s.Description = "Returns the runtime-editable configuration for the specified scheduled job.";
            s.Response(200, "Configuration returned");
            s.Response(401, "Not authenticated");
            s.Response(404, "Job not found");
        });
        Options(x => x.WithTags("System Administration"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var jobName = Route<string>("jobName")!;

        var result = await _scheduledJobAdminService.GetJobConfigurationAsync(jobName, ct);
        if (result is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(result, ct);
    }
}
