using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Authentication;
using Aonik.Platform.Contracts.Services.Identity;

using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Identity;

internal sealed class CompleteAccountAccessEndpoint(IAccountAccessService service, IAccountAccessIdentityProofAccessor proofs)
    : Endpoint<AccountAccessTokenRequest, AccountAccessResponse>
{
    public override void Configure()
    {
        Post(AccountAccessCompletionEndpointMetadata.Route);
        Options(x => x.WithMetadata(new AccountAccessCompletionEndpointMetadata()).RequireRateLimiting("identity-security"));
    }

    public override void OnBeforeValidate(AccountAccessTokenRequest req)
    {
        HttpContext.Response.Headers.CacheControl = "no-store";
        HttpContext.Response.Headers["Referrer-Policy"] = "no-referrer";
    }

    public override async Task HandleAsync(AccountAccessTokenRequest req, CancellationToken ct)
    {
        var proof = proofs.GetCurrent();
        if (proof is null || !await service.CompleteAsync(req.Token, proof, AccountAccessPurposes.PaidSetup, ct))
            await Send.StatusCodeAsync(StatusCodes.Status410Gone, ct);
        else await Send.OkAsync(new("complete"), ct);
    }
}
