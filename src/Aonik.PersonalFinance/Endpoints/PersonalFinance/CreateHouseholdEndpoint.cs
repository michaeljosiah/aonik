using FastEndpoints;
using Microsoft.AspNetCore.Http;

using Aonik.PersonalFinance.Contracts.Models;
using Aonik.PersonalFinance.Contracts.Services;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.PersonalFinance.Endpoints;

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
        Summary(s =>
        {
            s.Summary = "Create a household";
            s.Description = "Creates a new household group for the authenticated user, enabling shared personal finance tracking among household members.";
            s.Response(200, "Household created successfully");
            s.Response(400, "Invalid request");
            s.Response(401, "Not authenticated");
            s.Response(409, "Household already exists");
            s.Response(422, "Validation error");
        });
        Options(x => x.WithTags("Personal Finance"));
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
