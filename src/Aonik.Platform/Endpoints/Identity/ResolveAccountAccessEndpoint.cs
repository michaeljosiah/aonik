using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Authentication;
using Aonik.Platform.Contracts.Services.Identity;

using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Identity;

internal sealed class ResolveAccountAccessEndpoint(IAccountAccessService service) : Endpoint<AccountAccessTokenRequest, AccountAccessResponse>
{
    public override void Configure()
    {
        Post("/identity/account-access/resolve");
        AllowAnonymous();
        Options(x => x.RequireRateLimiting("identity-security"));
    }

    public override void OnBeforeValidate(AccountAccessTokenRequest req)
    {
        HttpContext.Response.Headers.CacheControl = "no-store";
        HttpContext.Response.Headers["Referrer-Policy"] = "no-referrer";
    }

    public override async Task HandleAsync(AccountAccessTokenRequest req, CancellationToken ct)
    {
        if (await service.ResolveAsync(req.Token, ct)) await Send.OkAsync(new("ready"), ct);
        else await Send.StatusCodeAsync(StatusCodes.Status410Gone, ct);
    }
}
