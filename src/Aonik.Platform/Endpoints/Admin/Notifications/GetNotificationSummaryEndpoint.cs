using Aonik.Platform.Contracts.Models.Notifications;
using Aonik.Platform.Contracts.Services.Notifications;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Notifications;

internal sealed class GetNotificationSummaryEndpoint : EndpointWithoutRequest<NotificationSummaryResponse>
{
    private readonly INotificationService _notificationService;

    public GetNotificationSummaryEndpoint(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public override void Configure()
    {
        Get("/admin/notifications/summary");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Get notification summary";
            s.Description = "Returns aggregated notification counts for the current user, including unread totals.";
            s.Response(200, "Notification summary");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Notifications"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await _notificationService.GetSummaryForCurrentUserAsync(ct);
        await Send.OkAsync(result, ct);
    }
}
