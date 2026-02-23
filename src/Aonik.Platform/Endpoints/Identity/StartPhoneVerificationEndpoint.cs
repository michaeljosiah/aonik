using Microsoft.AspNetCore.Http;
using FastEndpoints;
using Aonik.Platform.Contracts.Api.Identity;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Identity;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Platform.Endpoints.Identity;

public class StartPhoneVerificationEndpoint : Endpoint<StartPhoneVerificationRequest, VerificationChallengeResponse>
{
    private readonly IVerificationService _verificationService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;

    public StartPhoneVerificationEndpoint(
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
        Post("/v1/verifications/phone/start");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(StartPhoneVerificationRequest req, CancellationToken ct)
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

        var request = new PhoneVerificationStartRequest(req.Phone);
        var result = await _verificationService.StartPhoneVerificationAsync(userId, request.Phone, ct);

        await Send.OkAsync(MapResponse(result), ct);
    }

    private static VerificationChallengeResponse MapResponse(Aonik.Platform.Contracts.Models.Identity.VerificationChallengeResult result)
    {
        return new VerificationChallengeResponse(result.ChallengeId, result.ExpiresAt);
    }
}
