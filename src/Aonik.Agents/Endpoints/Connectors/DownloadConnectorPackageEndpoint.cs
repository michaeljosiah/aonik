using System.Text;

using FastEndpoints;

using Aonik.SharedKernel.Abstractions.Connectors;

namespace Aonik.Agents.Endpoints.Connectors;

public sealed class DownloadConnectorPackageEndpoint(IConnectorPackageCatalog catalog) : EndpointWithoutRequest
{
    public override void Configure() { Get("/integrations/catalog/packages/{packageKey}/{version}/artifact"); Policies("AdminUserPolicy"); }
    public override async Task HandleAsync(CancellationToken ct)
    {
        string key = Route<string>("packageKey")!; string version = Route<string>("version")!;
        string? artifact = await catalog.DownloadAsync(key, version, ct);
        if (artifact is null) { await Send.NotFoundAsync(ct); return; }
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(artifact));
        HttpContext.Response.Headers.CacheControl = "no-store";
        HttpContext.Response.Headers.XContentTypeOptions = "nosniff";
        await Send.StreamAsync(stream, $"{key}-{version}.json", contentType: "application/json", enableRangeProcessing: false, cancellation: ct);
    }
}
