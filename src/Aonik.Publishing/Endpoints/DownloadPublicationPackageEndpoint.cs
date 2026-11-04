using FastEndpoints;
using Microsoft.AspNetCore.Http;

using Aonik.Publishing.Contracts;
using Aonik.Publishing.Services;

namespace Aonik.Publishing.Endpoints;

internal sealed class DownloadPublicationPackageEndpoint(IPublicationIntakeService intake) : PublishingEndpoint<SubmissionPath, EmptyResponse>
{
    public override void Configure() { Get("/publishing/submissions/{Id:guid}/package"); Policies("UserPolicy"); Tags("Publishing"); }
    public override Task HandleAsync(SubmissionPath req, CancellationToken ct) => GuardedAsync(async () =>
    {
        var download = await intake.DownloadAsync(req.Id, ct);
        HttpContext.Response.Headers.CacheControl = "private, no-store";
        HttpContext.Response.Headers.XContentTypeOptions = "nosniff";
        HttpContext.Response.Headers.ContentSecurityPolicy = "default-src 'none'; sandbox";
        await Send.ResultAsync(Results.File(download.Content, "application/zip", download.FileName, enableRangeProcessing: false));
    });
}
