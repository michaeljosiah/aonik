using Microsoft.AspNetCore.Http;
using FastEndpoints;
using Aonik.Platform.Contracts.Api.Identity;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Identity;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Platform.Endpoints.Identity;

public class StartEmailVerificationEndpoint : Endpoint<StartEmailVerificationRequest, VerificationChallengeResponse>
{
    private readonly IVerificationService _verificationService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;

    public StartEmailVerificationEndpoint(
        IVerificationService verificationService,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider)
    {
        _verificationService = verificationService;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
    }

    public override void Configure()
    {
        Post("/v1/verifications/email/start");
        Policies("AdminUserPolicy");
        Summary(s =>
        {
            s.Summary = "Start email verification";
            s.Description = "Initiates email verification by sending an OTP code to the specified email address.";
            s.Response(200, "Verification challenge created");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Identity"));
    }

    public override async Task HandleAsync(StartEmailVerificationRequest req, CancellationToken ct)
    {
        if (!_currentUserProvider.TryGetCurrentUserId(out var userId))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await HttpContext.Response.WriteAsJsonAsync(new { error = "Authentication required." }, ct);
            return;
        }

        if (!_tenantProvider.TryGetCurrentTenantId(out _))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await HttpContext.Response.WriteAsJsonAsync(new { error = "Tenant context missing." }, ct);
            return;
        }

        var request = new EmailVerificationStartRequest(req.Email);
        var result = await _verificationService.StartEmailVerificationAsync(userId, request.Email, ct);

        await Send.OkAsync(MapResponse(result), ct);
    }

    private static VerificationChallengeResponse MapResponse(Aonik.Platform.Contracts.Models.Identity.VerificationChallengeResult result)
    {
        return new VerificationChallengeResponse(result.ChallengeId, result.ExpiresAt);
    }
}
