using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

using Aonik.Platform.Contracts.Api.Identity;
using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Identity;
using Aonik.SharedKernel.Abstractions.Multitenancy;

namespace Aonik.Platform.Endpoints.Identity;

public class ForgotPasswordEndpoint : Endpoint<ForgotPasswordRequestDto, ForgotPasswordResponseDto>
{
    private readonly IIdentityService _identityService;
    private readonly ITenantProvider _tenantProvider;

    public ForgotPasswordEndpoint(IIdentityService identityService, ITenantProvider tenantProvider)
    {
        _identityService = identityService;
        _tenantProvider = tenantProvider;
    }

    public override void Configure()
    {
        Post("/identity/password/forgot");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Request password reset email";
            s.Description = "Sends a password reset link to the specified email address if a matching account exists.";
            s.Response(200, "Request accepted; any eligible account receives provider instructions.");
        });
        Options(x => x.WithTags("Identity").RequireRateLimiting("identity-security"));
    }

    public override async Task HandleAsync(ForgotPasswordRequestDto req, CancellationToken ct)
    {
        HttpContext.Response.Headers.CacheControl = "no-store";
        HttpContext.Response.Headers["Referrer-Policy"] = "no-referrer";
        if (!_tenantProvider.TryGetCurrentTenantId(out var tenantId) || tenantId == Guid.Empty || tenantId != req.TenantId)
        {
            await Send.OkAsync(new ForgotPasswordResponseDto("ok"), ct);
            return;
        }
        var result = await _identityService.SendPasswordResetAsync(
            new ForgotPasswordRequest(req.Email, tenantId),
            ct);

        await Send.OkAsync(new ForgotPasswordResponseDto(result.Status), ct);
    }
}
