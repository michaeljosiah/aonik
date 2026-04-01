using System.Text.Json;
using System.Text.Json.Serialization;
using Aonik.Platform.Contracts.Services.Notifications;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aonik.Platform.Endpoints.Admin.Notifications;

public static class AdminNotificationStreamingEndpoint
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    public static IEndpointConventionBuilder MapAdminNotificationStreaming(
        this IEndpointRouteBuilder endpoints,
        string pattern = "/admin/notifications/stream")
    {
        return endpoints.MapGet(pattern, HandleStreamAsync)
            .WithName("AdminNotificationStreaming")
            .WithTags("Platform");
    }

    private static async Task HandleStreamAsync(HttpContext context)
    {
        var cancellationToken = context.RequestAborted;
        var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger("AdminNotificationStreaming");
        var currentUserProvider = context.RequestServices.GetRequiredService<ICurrentUserProvider>();
        var tenantProvider = context.RequestServices.GetRequiredService<ITenantProvider>();

        if (!currentUserProvider.TryGetCurrentUserId(out var userId))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Authentication required." }, cancellationToken);
            return;
        }

        if (!tenantProvider.TryGetCurrentTenantId(out var tenantId))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Tenant context missing." }, cancellationToken);
            return;
        }

        var notificationService = context.RequestServices.GetRequiredService<INotificationService>();
        var realtimePublisher = context.RequestServices.GetRequiredService<Services.Notifications.INotificationRealtimePublisher>();

        context.Response.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache,no-store";
        context.Response.Headers["Pragma"] = "no-cache";
        context.Response.Headers["X-Accel-Buffering"] = "no";

        await using var enumerator = realtimePublisher.SubscribeAsync(tenantId, userId, cancellationToken)
            .GetAsyncEnumerator(cancellationToken);

        var summary = await notificationService.GetSummaryForCurrentUserAsync(cancellationToken);
        await WriteSseEventAsync(context.Response, new
        {
            type = "HELLO",
            unreadCount = summary.UnreadCount,
            serverTimeUtc = DateTime.UtcNow,
        }, cancellationToken);

        try
        {
            var moveNextTask = enumerator.MoveNextAsync().AsTask();

            while (!cancellationToken.IsCancellationRequested)
            {
                var heartbeatTask = Task.Delay(TimeSpan.FromSeconds(15), cancellationToken);
                var completedTask = await Task.WhenAny(moveNextTask, heartbeatTask);

                if (completedTask == moveNextTask)
                {
                    if (!await moveNextTask)
                    {
                        break;
                    }

                    var notificationEvent = enumerator.Current;
                    await WriteSseEventAsync(context.Response, new
                    {
                        type = notificationEvent.Type,
                        notification = notificationEvent.Notification,
                        unreadCountDelta = notificationEvent.UnreadCountDelta,
                    }, cancellationToken);

                    moveNextTask = enumerator.MoveNextAsync().AsTask();
                    continue;
                }

                await WriteSseEventAsync(context.Response, new
                {
                    type = "HEARTBEAT",
                    serverTimeUtc = DateTime.UtcNow,
                }, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (IOException ex)
        {
            logger.LogDebug(ex, "Notification stream disconnected for tenant {TenantId} user {UserId}", tenantId, userId);
        }
    }

    private static async Task WriteSseEventAsync<T>(
        HttpResponse response,
        T payload,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        await response.WriteAsync($"data: {json}\n\n", cancellationToken);
        await response.Body.FlushAsync(cancellationToken);
    }
}
