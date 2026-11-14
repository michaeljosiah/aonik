using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

using Aonik.Platform.Services.Integrations;
using Aonik.SharedKernel.Abstractions.Connectors;

namespace Aonik.Platform.Endpoints.Integrations;

internal sealed class BeginConnectorConsentEndpoint(ConnectorConsentService consent, IConfiguration configuration) : Endpoint<ConnectionRequestCommand>
{
    internal const string CallbackPath = "/integrations/gmail/oauth/callback";
    internal static string CookieName(Guid requestId) => $"aonik_connector_consent_{requestId:N}";
    internal static string CookiePath(HttpContext context, IConfiguration configuration) =>
        Uri.TryCreate(configuration["Integrations:Gmail:OAuth:RedirectUri"], UriKind.Absolute, out Uri? uri) ? uri.AbsolutePath : context.Request.PathBase.Value + CallbackPath;
    public override void Configure() => Post("/v1/integrations/connection-requests/{RequestId:guid}/consent");
    public override async Task HandleAsync(ConnectionRequestCommand request, CancellationToken ct)
    {
        HttpContext.Response.Headers.CacheControl = "no-store";
        await Send.ResultAsync(await ConnectorHttp.ExecuteAsync(async () =>
        {
            ConnectorConsentChallenge challenge = await consent.BeginAsync(Route<Guid>("RequestId"), request, ct);
            HttpContext.Response.Cookies.Append(CookieName(challenge.RequestId), challenge.Cookie,
                new CookieOptions { HttpOnly = true, Secure = HttpContext.Request.IsHttps, SameSite = SameSiteMode.Lax,
                    Path = CookiePath(HttpContext, configuration), Expires = new DateTimeOffset(challenge.ExpiresAt) });
            return challenge;
        }));
    }
}
