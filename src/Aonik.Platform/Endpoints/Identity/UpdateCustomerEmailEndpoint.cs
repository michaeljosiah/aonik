using Aonik.Platform.Contracts.Api.Identity;
using Aonik.Platform.Contracts.Services.Authentication;
using Aonik.Platform.Contracts.Services.Identity;

using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Identity;

public sealed class UpdateCustomerEmailEndpoint(IAccountAccessService service, IAccountAccessIdentityProofAccessor proofs)
    : Endpoint<UpdateCustomerEmailRequest>
{
    public override void Configure()
    {
        Put("/profiles/customers/me/email");
        Policies("AdminUserPolicy");
        Summary(s => s.Summary = "Request a new-email confirmation link using recent identity-provider authentication.");
        Options(x => x.WithTags("Identity").RequireRateLimiting("identity-security"));
    }

    public override void OnBeforeValidate(UpdateCustomerEmailRequest req)
    {
        HttpContext.Response.Headers.CacheControl = "no-store";
        HttpContext.Response.Headers["Referrer-Policy"] = "no-referrer";
    }

    public override async Task HandleAsync(UpdateCustomerEmailRequest req, CancellationToken ct)
    {
        var proof = proofs.GetCurrent();
        if (proof is not null) await service.RequestEmailChangeAsync(req.NewEmail, proof, ct);
        await Send.StatusCodeAsync(StatusCodes.Status202Accepted, ct);
    }
}
