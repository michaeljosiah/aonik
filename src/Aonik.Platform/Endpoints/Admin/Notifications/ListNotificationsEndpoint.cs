using Aonik.Platform.Contracts.Models.Notifications;
using Aonik.Platform.Contracts.Services.Notifications;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Notifications;

internal sealed record ListNotificationsRequest
{
    [QueryParam]
    public string? Status { get; init; }

    [QueryParam]
    public int Take { get; init; } = 50;

    [QueryParam]
    public DateTime? Before { get; init; }

    [QueryParam]
    public bool IncludeDismissed { get; init; }
}

internal sealed class ListNotificationsEndpoint : Endpoint<ListNotificationsRequest, List<NotificationResponse>>
{
    private readonly INotificationService _notificationService;

    public ListNotificationsEndpoint(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public override void Configure()
    {
        Get("/admin/notifications");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "List notifications";
            s.Description = "Returns a list of notifications for the current user with optional status filtering and cursor-based pagination.";
            s.Response(200, "Notification list");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Notifications"));
    }

    public override async Task HandleAsync(ListNotificationsRequest req, CancellationToken ct)
    {
        var result = await _notificationService.ListForCurrentUserAsync(
            new NotificationListRequest(req.Status, req.Take, req.Before, req.IncludeDismissed),
            ct);

        await Send.OkAsync(result, ct);
    }
}
