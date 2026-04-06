using FastEndpoints;

using Aonik.Platform.Contracts.Api.Identity;
using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Identity;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Identity;

public class ForgotPasswordEndpoint : Endpoint<ForgotPasswordRequestDto, ForgotPasswordResponseDto>
{
    private readonly IIdentityService _identityService;

    public ForgotPasswordEndpoint(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    public override void Configure()
    {
        Post("/identity/password/forgot");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Request password reset email";
            s.Description = "Sends a password reset link to the specified email address if a matching account exists.";
            s.Response(200, "Reset email sent");
        });
        Options(x => x.WithTags("Identity"));
    }

    public override async Task HandleAsync(ForgotPasswordRequestDto req, CancellationToken ct)
    {
        var result = await _identityService.SendPasswordResetAsync(
            new ForgotPasswordRequest(req.Email, req.TenantId),
            ct);

        await Send.OkAsync(new ForgotPasswordResponseDto(result.Status), ct);
    }
}
