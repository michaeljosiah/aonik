using FastEndpoints;
using Microsoft.AspNetCore.Http;

using Aonik.Platform.Contracts.Api.PersonalFinance;
using Aonik.Platform.Contracts.Models.Settings;
using Aonik.Platform.Contracts.Services.Settings;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;

namespace Aonik.Platform.Endpoints.PersonalFinance;

public class GetPayaboSetupProfileEndpoint : EndpointWithoutRequest<PayaboSetupProfileResponse>
{
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IPayaboSetupProfileService _service;

    public GetPayaboSetupProfileEndpoint(
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
        Get("/personal-finance/setup-profile");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Get Payabo setup profile";
            s.Description = "Returns the current user's Payabo onboarding setup profile including use cases, goals, and completion status.";
            s.Response(200, "Setup profile returned");
            s.Response(401, "Not authenticated");
            s.Response(404, "Setup profile not found");
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

        var profile = await _service.GetCurrentAsync(ct);
        if (profile == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(Map(profile), ct);
    }

    internal static PayaboSetupProfileResponse Map(PayaboSetupProfileSnapshot profile)
    {
        return new PayaboSetupProfileResponse(
            profile.SelectedUseCases,
            profile.AccountSourceTypes,
            profile.ConnectChoice,
            profile.Responsibilities,
            profile.SupportType,
            profile.FinancialGoals,
            profile.Completed);
    }
}
