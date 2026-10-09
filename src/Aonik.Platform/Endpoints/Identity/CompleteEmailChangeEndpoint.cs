using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Authentication;
using Aonik.Platform.Contracts.Services.Identity;

using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Identity;

internal sealed class CompleteEmailChangeEndpoint(IAccountAccessService service, IAccountAccessIdentityProofAccessor proofs)
    : Endpoint<AccountAccessTokenRequest, AccountAccessResponse>
{
    public override void Configure()
    {
        Post("/identity/email-change/complete");
        Policies("AdminUserPolicy");
        Options(x => x.RequireRateLimiting("identity-security"));
    }

    public override void OnBeforeValidate(AccountAccessTokenRequest req)
    {
        HttpContext.Response.Headers.CacheControl = "no-store";
        HttpContext.Response.Headers["Referrer-Policy"] = "no-referrer";
    }

    public override async Task HandleAsync(AccountAccessTokenRequest req, CancellationToken ct)
    {
        try
        {
            var proof = proofs.GetCurrent();
            if (proof is null || !await service.CompleteAsync(req.Token, proof, AccountAccessPurposes.EmailChange, ct))
                await Send.StatusCodeAsync(StatusCodes.Status410Gone, ct);
            else await Send.OkAsync(new("complete"), ct);
        }
        catch (Exception exception) when (exception is InvalidOperationException or HttpRequestException
            || (exception is OperationCanceledException && !ct.IsCancellationRequested))
        {
            // Applying remains recoverable; do not expose provider errors or identity details.
            await Send.StatusCodeAsync(StatusCodes.Status503ServiceUnavailable, ct);
        }
    }
}
