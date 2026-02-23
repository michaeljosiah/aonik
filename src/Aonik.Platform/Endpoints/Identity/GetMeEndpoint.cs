using Microsoft.AspNetCore.Http;
using System.Text.Json;
using FastEndpoints;

using Aonik.Platform.Contracts.Api.Identity;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Observability;
using Aonik.Platform.Services.Compliance;
using Aonik.Platform.Contracts.Services.Compliance;
using Aonik.Platform.Contracts.Services.Identity;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Platform.Endpoints.Identity;

public class GetMeEndpoint : EndpointWithoutRequest<CurrentUserResponse>
{
    private readonly IUserProfileService _userProfileService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IAuditLogWriter _auditLogWriter;
    private readonly ICorrelationContext _correlationContext;

    public GetMeEndpoint(
        IUserProfileService userProfileService,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider,
        IAuditLogWriter auditLogWriter,
        ICorrelationContext correlationContext)
    {
        _userProfileService = userProfileService;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
        _auditLogWriter = auditLogWriter;
        _correlationContext = correlationContext;
    }

    public override void Configure()
    {
        Get("/v1/me");
        Policies("AdminUserPolicy");
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
            AuditEventNames.CurrentUserViewed,
            "User",
            result.UserId,
            tenantId,
            userId,
            _correlationContext.CorrelationId,
            JsonSerializer.Serialize(new { result.UserId, TenantId = tenantId }),
            ct);

        await Send.OkAsync(MapResponse(result), ct);
    }

    private static CurrentUserResponse MapResponse(Aonik.Platform.Contracts.Models.Identity.CurrentUserSnapshot snapshot)
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
