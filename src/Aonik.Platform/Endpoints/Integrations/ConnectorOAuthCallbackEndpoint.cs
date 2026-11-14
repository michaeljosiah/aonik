using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

using Aonik.Platform.Entities.Identity;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Integrations;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Connectors;
using Aonik.SharedKernel.Abstractions.Multitenancy;

namespace Aonik.Platform.Endpoints.Integrations;

internal sealed record ConnectorCallbackHttpRequest(string? State, string? Code, string? Error);
internal sealed class ConnectorOAuthCallbackEndpoint(ConnectorConsentService consent, ITenantContext tenantContext,
    PlatformDbContext database, ICurrentUserProvider currentUser, IConfiguration configuration) : Endpoint<ConnectorCallbackHttpRequest>
{
    public override void Configure() { Get(BeginConnectorConsentEndpoint.CallbackPath); AllowAnonymous(); }
    public override async Task HandleAsync(ConnectorCallbackHttpRequest request, CancellationToken ct)
    {
        HttpContext.Response.Headers.CacheControl = "no-store";
        HttpContext.Response.Headers["Referrer-Policy"] = "no-referrer";
        if (request.State is null || request.Code is null || request.Error is not null
            || !Guid.TryParseExact(request.State.Split('.')[0], "N", out Guid requestId)
            || !HttpContext.Request.Cookies.TryGetValue(BeginConnectorConsentEndpoint.CookieName(requestId), out string? cookie))
        { await Send.ResultAsync(Results.BadRequest(new { code = "ConsentInvalid" })); return; }
        HttpContext.Response.Cookies.Delete(BeginConnectorConsentEndpoint.CookieName(requestId),
            new CookieOptions { Path = BeginConnectorConsentEndpoint.CookiePath(HttpContext, configuration), HttpOnly = true, Secure = HttpContext.Request.IsHttps, SameSite = SameSiteMode.Lax });
        try
        {
            ConnectorConsentProof proof = consent.ReadProof(cookie, request.State);
            if (HttpContext.User.Identity?.IsAuthenticated == true && tenantContext.TenantId != proof.TenantId)
                throw new PermissionDeniedException("Integration.Use");
            // Caller headers cannot select a callback tenant. Only the protected owner proof can.
            tenantContext.TenantId = proof.TenantId; tenantContext.ResolutionSource = "connector-consent-proof";
            if (!await database.Tenants.AsNoTracking().AnyAsync(tenant => tenant.Id == proof.TenantId && tenant.Status == TenantStatus.Active, ct))
                throw new PermissionDeniedException("Integration.Use");
            await consent.ReceiveCallbackAsync(cookie, request.State, request.Code,
                HttpContext.User.Identity?.IsAuthenticated == true ? currentUser.GetCurrentUserId() : null, ct);
            string returnUri = configuration["Integrations:Gmail:OAuth:ReviewReturnUri"]!;
            // The provider validates this deployment-owned URI before exchanging any code.
            await Send.ResultAsync(Results.Redirect(QueryHelpers.AddQueryString(returnUri, "requestId", requestId.ToString("D"))));
        }
        catch (Exception exception) when (exception is ConnectorAccessException or ConnectorProviderException or PermissionDeniedException)
        { await Send.ResultAsync(Results.BadRequest(new { code = "ConsentNotApplied", error = "Sign in to review this connection request and restart consent if needed." })); }
    }
}
