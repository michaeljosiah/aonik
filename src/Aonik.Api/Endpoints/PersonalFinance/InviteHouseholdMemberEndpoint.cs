using FastEndpoints;
using Microsoft.AspNetCore.Http;

using Aonik.Api.Contracts.PersonalFinance;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Application.Services.PersonalFinance;
using Aonik.SharedKernel.Abstractions;
using AppModels = Aonik.Application.Models.PersonalFinance;

namespace Aonik.Api.Endpoints.PersonalFinance;

public class InviteHouseholdMemberEndpoint : Endpoint<InviteHouseholdMemberRequest, HouseholdMemberResponse>
{
    private readonly IHouseholdService _householdService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;

    public InviteHouseholdMemberEndpoint(
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
        Post("/personal-finance/households/{householdId:guid}/members/invite");
        Policies("UserPolicy");
    }

    public override async Task HandleAsync(InviteHouseholdMemberRequest req, CancellationToken ct)
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

        var householdId = Route<Guid>("householdId");
        if (householdId == Guid.Empty)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsJsonAsync(new { error = "householdId is required." }, ct);
            return;
        }

        if (req.UserId == Guid.Empty || string.IsNullOrWhiteSpace(req.Role))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsJsonAsync(new { error = "userId and role are required." }, ct);
            return;
        }

        try
        {
            var appRequest = new AppModels.InviteHouseholdMemberRequest(
                householdId,
                req.UserId,
                req.Role,
                req.Permissions);

            var result = await _householdService.InviteMemberAsync(appRequest, ct);

            await Send.OkAsync(new HouseholdMemberResponse(
                result.MemberId,
                result.HouseholdId,
                result.UserId,
                result.Role,
                result.Permissions,
                result.CreatedAt), ct);
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
