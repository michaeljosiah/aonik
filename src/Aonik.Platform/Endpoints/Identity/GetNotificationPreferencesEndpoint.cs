using Microsoft.AspNetCore.Http;
using FastEndpoints;

using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Platform.Contracts.Services.Identity;
using Aonik.SharedKernel.Abstractions;

using ApiNotificationPreferencesResponse = Aonik.Platform.Contracts.Api.Identity.NotificationPreferencesResponse;

namespace Aonik.Platform.Endpoints.Identity;

public class GetNotificationPreferencesEndpoint : EndpointWithoutRequest<ApiNotificationPreferencesResponse>
{
    private readonly IUserProfileService _userProfileService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;

    public GetNotificationPreferencesEndpoint(
        IUserProfileService userProfileService,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider)
    {
        _userProfileService = userProfileService;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
    }

    public override void Configure()
    {
        Get("/profiles/customers/me/notifications");
        Policies("AdminUserPolicy");
        Summary(s =>
        {
            s.Summary = "Get notification preferences";
            s.Description = "Returns the current customer's push and email notification preferences.";
            s.Response(200, "Preferences returned");
            s.Response(401, "Not authenticated");
            s.Response(404, "Customer not found");
        });
        Options(x => x.WithTags("Identity"));
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

        var result = await _userProfileService.GetNotificationPreferencesAsync(userId, tenantId, ct);
        if (result == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(MapResponse(result), ct);
    }

    private static ApiNotificationPreferencesResponse MapResponse(
        Contracts.Models.Identity.NotificationPreferencesResponse prefs)
    {
        return new ApiNotificationPreferencesResponse(
            prefs.Email,
            prefs.NewBillsPush,
            prefs.BillUpdatesPush,
            prefs.BillAssistPush,
            prefs.MbaMessagesPush,
            prefs.OrgMessagesPush,
            prefs.FriendsMessagesPush,
            prefs.NewBillsEmail,
            prefs.BillUpdatesEmail,
            prefs.BillAssistEmail,
            prefs.MbaMessagesEmail,
            prefs.OrgMessagesEmail);
    }
}
