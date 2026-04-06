using FastEndpoints;
using Microsoft.AspNetCore.Http;

using Aonik.Platform.Contracts.Api.PersonalFinance;
using Aonik.Platform.Contracts.Models.Settings;
using Aonik.Platform.Contracts.Services.Settings;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;

namespace Aonik.Platform.Endpoints.PersonalFinance;

public class PutPayaboSetupProfileEndpoint : Endpoint<PayaboSetupProfileRequest, PayaboSetupProfileResponse>
{
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IPayaboSetupProfileService _service;

    public PutPayaboSetupProfileEndpoint(
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
        Put("/personal-finance/setup-profile");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Save Payabo setup profile";
            s.Description = "Creates or updates the current user's Payabo onboarding setup profile with selected use cases, goals, and preferences.";
            s.Response(200, "Setup profile saved");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(PayaboSetupProfileRequest req, CancellationToken ct)
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

        var saved = await _service.SaveCurrentAsync(
            new PayaboSetupProfileSnapshot(
                req.SelectedUseCases,
                req.AccountSourceTypes,
                req.ConnectChoice,
                req.Responsibilities,
                req.SupportType,
                req.FinancialGoals,
                req.Completed),
            ct);

        await Send.OkAsync(GetPayaboSetupProfileEndpoint.Map(saved), ct);
    }
}
