using FastEndpoints;
using Microsoft.AspNetCore.Http;

using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Finance.Endpoints.PersonalFinance;

internal class CreateHouseholdEndpoint : Endpoint<CreateHouseholdRequest, HouseholdResponse>
{
    private readonly IHouseholdService _householdService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;

    public CreateHouseholdEndpoint(
        IHouseholdService householdService,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider)
    {
        _householdService = householdService;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
    }

    public override void Configure()
    {
        Post("/personal-finance/households");
        Policies("UserPolicy");
    }

    public override async Task HandleAsync(CreateHouseholdRequest req, CancellationToken ct)
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

        if (string.IsNullOrWhiteSpace(req.Name))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsJsonAsync(new { error = "name is required." }, ct);
            return;
        }

        try
        {
            var result = await _householdService.CreateHouseholdAsync(req, ct);
            await Send.OkAsync(result, ct);
        }
        catch (ArgumentException ex)
        {
            HttpContext.Response.StatusCode = 422;
            await HttpContext.Response.WriteAsJsonAsync(new { error = ex.Message }, ct);
        }
        catch (InvalidOperationException ex)
        {
            HttpContext.Response.StatusCode = 409;
            await HttpContext.Response.WriteAsJsonAsync(new { error = ex.Message }, ct);
        }
    }
}
