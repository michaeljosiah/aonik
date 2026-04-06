using Microsoft.AspNetCore.Http;
using FastEndpoints;
using Aonik.Platform.Contracts.Api.Identity;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Identity;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Platform.Endpoints.Identity;

public class ConfirmPhoneVerificationEndpoint : Endpoint<ConfirmPhoneVerificationRequest, VerificationConfirmationResponse>
{
    private readonly IVerificationService _verificationService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;

    public ConfirmPhoneVerificationEndpoint(
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
        Post("/v1/verifications/phone/confirm");
        Policies("AdminUserPolicy");
        Summary(s =>
        {
            s.Summary = "Confirm phone verification code";
            s.Description = "Validates the OTP code sent to the user's phone number and marks the phone as verified.";
            s.Response(200, "Verification result returned");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Identity"));
    }

    public override async Task HandleAsync(ConfirmPhoneVerificationRequest req, CancellationToken ct)
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

        var request = new PhoneVerificationConfirmRequest(req.Phone, req.Code);
        var isVerified = await _verificationService.ConfirmPhoneVerificationAsync(
            userId,
            request.Phone,
            request.Code,
            ct);

        await Send.OkAsync(new VerificationConfirmationResponse(isVerified), ct);
    }
}
