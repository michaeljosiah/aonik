using FastEndpoints;
using Microsoft.AspNetCore.Http;

using Aonik.Platform.Contracts.Api.PersonalFinance;
using Aonik.Platform.Contracts.Services.Settings;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;

namespace Aonik.Platform.Endpoints.PersonalFinance;

public class DeletePayaboSetupProfileEndpoint : EndpointWithoutRequest<ClearPayaboSetupProfileResponse>
{
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IPayaboSetupProfileService _service;

    public DeletePayaboSetupProfileEndpoint(
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider,
        IPayaboSetupProfileService service)
    {
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
        _service = service;
    }

    public override void Configure()
    {
        Delete("/personal-finance/setup-profile");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Clear Payabo setup profile";
            s.Description = "Deletes the current user's Payabo onboarding setup profile, allowing them to restart the setup flow.";
            s.Response(200, "Setup profile cleared");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!_currentUserProvider.TryGetCurrentUserId(out _))
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

        await _service.ClearCurrentAsync(ct);
        await Send.OkAsync(new ClearPayaboSetupProfileResponse("ok"), ct);
    }
}
