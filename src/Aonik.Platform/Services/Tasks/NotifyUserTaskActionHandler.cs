using System.Text.Json;
using Aonik.Platform.Contracts.Models.Notifications;
using Aonik.Platform.Contracts.Services.Notifications;
using Aonik.Platform.Notifications;
using Aonik.SharedKernel.Abstractions.Tasks;

namespace Aonik.Platform.Services.Tasks;

/// <summary>
/// Reference <see cref="ITaskActionHandler"/> for <c>notify_user</c> (Spec 034) — the
/// low-risk action: post an in-app notification to a user when a task is due. Runs
/// in-band (reversible, no money), records its result, and is the action a
/// "remind me" task fires. Registered keyed by <see cref="TaskActionTypes.NotifyUser"/>.
/// </summary>
internal sealed class NotifyUserTaskActionHandler : ITaskActionHandler
{
    private static readonly JsonSerializerOptions PayloadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly INotificationService _notificationService;

    public NotifyUserTaskActionHandler(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public string ActionType => TaskActionTypes.NotifyUser;

    public async Task<TaskActionResult> ExecuteAsync(TaskActionContext context, CancellationToken cancellationToken = default)
    {
        NotifyUserPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<NotifyUserPayload>(context.ActionPayloadJson, PayloadOptions);
        }
        catch (JsonException ex)
        {
            return new TaskActionResult(TaskActionOutcome.Failed, Error: $"Invalid notify_user payload: {ex.Message}");
        }

        if (payload is null)
        {
            return new TaskActionResult(TaskActionOutcome.Failed, Error: "notify_user payload was empty.");
        }

        // Target the payload user, falling back to the task assignee.
        var userId = payload.UserId != Guid.Empty ? payload.UserId : context.AssigneeId ?? Guid.Empty;
        if (userId == Guid.Empty)
        {
            return new TaskActionResult(TaskActionOutcome.Failed, Error: "notify_user requires a target userId.");
        }

        if (string.IsNullOrWhiteSpace(payload.Title) || string.IsNullOrWhiteSpace(payload.Body))
        {
            return new TaskActionResult(TaskActionOutcome.Failed, Error: "notify_user requires a title and body.");
        }

        var notification = await _notificationService.CreateForUserAsync(
            new CreateNotificationRequest(
                TenantId: context.TenantId,
                UserId: userId,
                Type: string.IsNullOrWhiteSpace(payload.Type) ? "task.reminder" : payload.Type!,
                Source: "TaskScheduler",
                Title: payload.Title,
                Body: payload.Body,
                Severity: NormalizeSeverity(payload.Severity),
                ActionUrl: payload.ActionUrl,
                CorrelationId: context.WorkItemId.ToString(),
                AiRunId: null),
            cancellationToken).ConfigureAwait(false);

        var resultJson = JsonSerializer.Serialize(new { notificationId = notification.Id });
        return new TaskActionResult(TaskActionOutcome.Succeeded, ResultJson: resultJson);
    }

    // Case-insensitive: the payload is untrusted (agent/end-user authored), so "warning"/"ERROR"
    // must map to the canonical PascalCase constant rather than silently degrading to Info.
    private static string NormalizeSeverity(string? severity) => severity?.Trim().ToLowerInvariant() switch
    {
        "info" => NotificationSeverities.Info,
        "success" => NotificationSeverities.Success,
        "warning" => NotificationSeverities.Warning,
        "error" => NotificationSeverities.Error,
        _ => NotificationSeverities.Info,
    };

    private sealed record NotifyUserPayload(
        Guid UserId,
        string Title,
        string Body,
        string? Severity = null,
        string? Type = null,
        string? ActionUrl = null);
}
