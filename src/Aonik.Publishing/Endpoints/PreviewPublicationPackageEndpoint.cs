using FastEndpoints;

using Aonik.Publishing.Contracts;
using Aonik.Publishing.Services;

namespace Aonik.Publishing.Endpoints;

internal sealed class PreviewPublicationPackageEndpoint(IPublicationIntakeService intake) : PublishingEndpoint<SubmissionPath, ValidationPreview>
{
    public override void Configure() { Get("/publishing/submissions/{Id:guid}/preview"); Policies("UserPolicy"); Tags("Publishing"); }
    public override Task HandleAsync(SubmissionPath req, CancellationToken ct) => GuardedAsync(async () =>
    {
        HttpContext.Response.Headers.CacheControl = "private, no-store";
        var preview = await intake.PreviewAsync(req.Id, ct);
        if (preview is null) await Send.NotFoundAsync(ct); else await Send.OkAsync(preview, ct);
    });
}
