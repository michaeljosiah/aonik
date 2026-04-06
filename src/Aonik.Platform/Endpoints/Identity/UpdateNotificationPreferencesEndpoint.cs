using Microsoft.AspNetCore.Http;
using FastEndpoints;

using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Identity;
using Aonik.SharedKernel.Abstractions;

using ApiUpdateNotificationPreferencesRequest = Aonik.Platform.Contracts.Api.Identity.UpdateNotificationPreferencesRequest;
using ApiNotificationPreferencesResponse = Aonik.Platform.Contracts.Api.Identity.NotificationPreferencesResponse;

namespace Aonik.Platform.Endpoints.Identity;

public class UpdateNotificationPreferencesEndpoint : Endpoint<ApiUpdateNotificationPreferencesRequest, ApiNotificationPreferencesResponse>
{
    private readonly IUserProfileService _userProfileService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;

    public UpdateNotificationPreferencesEndpoint(
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
        Put("/profiles/customers/me/notifications");
        Policies("AdminUserPolicy");
        Summary(s =>
        {
            s.Summary = "Update notification preferences";
            s.Description = "Updates the current customer's push and email notification preferences.";
            s.Response(200, "Preferences updated");
            s.Response(401, "Not authenticated");
            s.Response(404, "Customer not found");
        });
        Options(x => x.WithTags("Identity"));
    }

    public override async Task HandleAsync(ApiUpdateNotificationPreferencesRequest req, CancellationToken ct)
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

        var updateRequest = new UpdateNotificationPreferencesRequest(
            req.Email,
            req.NewBillsPush,
            req.BillUpdatesPush,
            req.BillAssistPush,
            req.MbaMessagesPush,
            req.OrgMessagesPush,
            req.FriendsMessagesPush,
            req.NewBillsEmail,
            req.BillUpdatesEmail,
            req.BillAssistEmail,
            req.MbaMessagesEmail,
            req.OrgMessagesEmail);

        var result = await _userProfileService.UpdateNotificationPreferencesAsync(userId, tenantId, updateRequest, ct);
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
