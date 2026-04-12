using Aonik.Platform.Contracts.Api.Jobs;
using Aonik.Platform.Contracts.Services.Operations;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Jobs;

/// <summary>
/// Updates the runtime configuration for a scheduled job.
/// </summary>
internal class UpdateScheduledJobConfigurationEndpoint : Endpoint<UpdateScheduledJobConfigurationRequest, ScheduledJobConfigurationResponse>
{
    private readonly IScheduledJobAdminService _scheduledJobAdminService;

    public UpdateScheduledJobConfigurationEndpoint(IScheduledJobAdminService scheduledJobAdminService)
    {
        _scheduledJobAdminService = scheduledJobAdminService;
    }

    public override void Configure()
    {
        Put("/admin/jobs/scheduled/{jobName}/configuration");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Update scheduled job configuration";
            s.Description = "Updates the runtime-editable configuration for the specified scheduled job. The job reads this configuration at execution time.";
            s.Response(200, "Configuration updated");
            s.Response(401, "Not authenticated");
            s.Response(404, "Job not found");
        });
        Options(x => x.WithTags("System Administration"));
    }

    public override async Task HandleAsync(UpdateScheduledJobConfigurationRequest req, CancellationToken ct)
    {
        var jobName = Route<string>("jobName")!;

        var result = await _scheduledJobAdminService.UpdateJobConfigurationAsync(jobName, req.ConfigurationJson, ct);
        if (result is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(result, ct);
    }
}
