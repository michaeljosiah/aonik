using FastEndpoints;
using Aonik.Api.Contracts.Identity;
using Aonik.Application.Abstractions.Multitenancy;
using Aonik.Application.Models.Identity;
using Aonik.Application.Services.Identity;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Api.Endpoints.Identity;

public class ConfirmEmailVerificationEndpoint : Endpoint<ConfirmEmailVerificationRequest, VerificationConfirmationResponse>
{
    private readonly IVerificationService _verificationService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;

    public ConfirmEmailVerificationEndpoint(
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
        Post("/v1/verifications/email/confirm");
        Policies("UserPolicy");
    }

    public override async Task HandleAsync(ConfirmEmailVerificationRequest req, CancellationToken ct)
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

        var request = new EmailVerificationConfirmRequest(req.Email, req.Code);
        var isVerified = await _verificationService.ConfirmEmailVerificationAsync(
            userId,
            request.Email,
            request.Code,
            ct);

        await Send.OkAsync(new VerificationConfirmationResponse(isVerified), ct);
    }
}
