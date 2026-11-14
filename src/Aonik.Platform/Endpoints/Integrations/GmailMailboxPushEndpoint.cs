using System.Text;

using FastEndpoints;
using Microsoft.AspNetCore.Http;

using Aonik.SharedKernel.Abstractions.Connectors;

namespace Aonik.Platform.Endpoints.Integrations;

internal sealed class GmailMailboxPushEndpoint(IConnectorMailboxPushIngress ingress) : EndpointWithoutRequest
{
    public override void Configure() { Post("/integrations/gmail/notifications"); AllowAnonymous(); }
    public override async Task HandleAsync(CancellationToken ct)
    {
        HttpContext.Response.Headers.CacheControl = "no-store";
        if (HttpContext.Request.ContentLength > 32_768) { await Send.ResultAsync(Results.StatusCode(413)); return; }
        using var output = new MemoryStream();
        byte[] buffer = new byte[4096];
        int read;
        while ((read = await HttpContext.Request.Body.ReadAsync(buffer, ct)) > 0)
        {
            if (output.Length + read > 32_768) { await Send.ResultAsync(Results.StatusCode(413)); return; }
            output.Write(buffer, 0, read);
        }
        try
        {
            await ingress.AcceptAsync(HttpContext.Request.Headers.Authorization.ToString(), Encoding.UTF8.GetString(output.ToArray()), ct);
            await Send.NoContentAsync(ct);
        }
        catch (ConnectorAccessException) { await Send.UnauthorizedAsync(ct); }
    }
}
