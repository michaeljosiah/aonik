using FastEndpoints;
using Microsoft.AspNetCore.Http;

using Aonik.Platform.Services.Integrations;
using Aonik.SharedKernel.Abstractions.Connectors;

namespace Aonik.Platform.Endpoints.Integrations;

internal sealed record OpenConnectorContentHttpRequest(string FileReference);
internal sealed class OpenConnectorContentEndpoint(ConnectorService connectors) : Endpoint<OpenConnectorContentHttpRequest>
{
    public override void Configure() => Post("/v1/integrations/content/download");
    public override async Task HandleAsync(OpenConnectorContentHttpRequest request, CancellationToken ct)
    {
        HttpContext.Response.Headers.CacheControl = "no-store";
        HttpContext.Response.Headers["X-Content-Type-Options"] = "nosniff";
        try
        {
            var content = await connectors.OpenContentAsync(request.FileReference, ct);
            await Send.ResultAsync(Results.File(content.Stream, content.Metadata.ContentType, content.Metadata.FileName, enableRangeProcessing: false));
        }
        catch (ConnectorAccessException exception) { await Send.ResultAsync(Results.Json(new { code = exception.Code }, statusCode: 403)); }
    }
}
