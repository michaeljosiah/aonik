using System.Text.Json;
using FastEndpoints;
using Aonik.Api.Contracts.Identity;
using Aonik.Application.Abstractions.Multitenancy;
using Aonik.Application.Services.Compliance;
using Aonik.Application.Services.Identity;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Api.Endpoints.Identity;

public class GetMeEndpoint : EndpointWithoutRequest<CurrentUserResponse>
{
    private readonly IUserProfileService _userProfileService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IAuditLogWriter _auditLogWriter;

    public GetMeEndpoint(
        IUserProfileService userProfileService,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider,
        IAuditLogWriter auditLogWriter)
    {
        _userProfileService = userProfileService;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
        _auditLogWriter = auditLogWriter;
    }

    public override void Configure()
    {
        Get("/v1/me");
        Policies("Users.Read");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!_currentUserProvider.TryGetCurrentUserId(out var userId))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await HttpContext.Response.WriteAsJsonAsync(new { error = "Authentication required." }, ct);
            return;
        }

        if (!_tenantProvider.TryGetCurrentTenantId(out var tenantId))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await HttpContext.Response.WriteAsJsonAsync(new { error = "Tenant context missing." }, ct);
            return;
        }

        var result = await _userProfileService.GetCurrentUserAsync(userId, tenantId, ct);

        if (result == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await _auditLogWriter.LogAsync(
            "CurrentUserViewed",
            "User",
            result.UserId,
            JsonSerializer.Serialize(new { result.UserId, result.TenantId }),
            ct);

        await Send.OkAsync(MapResponse(result), ct);
    }

    private static CurrentUserResponse MapResponse(Application.Models.Identity.CurrentUserSnapshot snapshot)
    {
        return new CurrentUserResponse(
            snapshot.UserId,
            snapshot.TenantId,
            snapshot.Email,
            snapshot.Phone,
            snapshot.Status,
            snapshot.PartyId,
            snapshot.DisplayName);
    }
}
