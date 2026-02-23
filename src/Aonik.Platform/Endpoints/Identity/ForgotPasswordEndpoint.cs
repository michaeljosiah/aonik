using FastEndpoints;

using Aonik.Platform.Contracts.Api.Identity;
using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Identity;

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
    }

    public override async Task HandleAsync(ForgotPasswordRequestDto req, CancellationToken ct)
    {
        var result = await _identityService.SendPasswordResetAsync(
            new ForgotPasswordRequest(req.Email, req.TenantId),
            ct);

        await Send.OkAsync(new ForgotPasswordResponseDto(result.Status), ct);
    }
}
