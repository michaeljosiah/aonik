using Aonik.Platform.Contracts.Models.Notifications;
using Aonik.Platform.Contracts.Services.Notifications;
using Aonik.Platform.Notifications;
using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Events;
using Aonik.SharedKernel.Events.Integration;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aonik.Platform.IntegrationEvents;

/// <summary>
/// Surfaces a newly raised Commerce low-stock alert through the Spec 016 admin realtime inbox
/// (Spec 052 §10). Commerce cannot reference <see cref="INotificationService"/> directly, so it
/// enqueues <see cref="LowStockAlertRaisedEvent"/> and this Platform-side handler creates the
/// notifications — one per active TenantAdmin of the alert's tenant. Runs in the Worker via the
/// outbox dispatcher; idempotent under re-delivery because each notification is keyed
/// <c>low-stock-{alertId}</c> and <see cref="INotificationService.CreateForUserAsync"/> dedupes
/// atomically per (tenant, user, key).
/// </summary>
internal sealed class LowStockAlertNotificationHandler : IEventHandler<LowStockAlertRaisedEvent>
{
    private const string TenantAdminRoleName = "TenantAdmin";
    private const string ActiveUserStatus = "Active";

    private readonly PlatformDbContext _dbContext;
    private readonly INotificationService _notificationService;
    private readonly ILogger<LowStockAlertNotificationHandler> _logger;

    public LowStockAlertNotificationHandler(
        PlatformDbContext dbContext,
        INotificationService notificationService,
        ILogger<LowStockAlertNotificationHandler> logger)
    {
        _dbContext = dbContext;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task HandleAsync(LowStockAlertRaisedEvent @event, CancellationToken cancellationToken = default)
    {
        // Recipients: the alert tenant's active TenantAdmin users — the same role-join shape as
        // PlatformAdminAlertAudienceResolver, scoped to the tenant instead of the platform.
        // AcrossTenants() is IgnoreQueryFilters(), which also drops the soft-delete filter — every
        // hop excludes deleted rows explicitly (a revoked assignment is a soft-deleted UserRole).
        var recipients = await _dbContext.UserRoles
            .AcrossTenants()
            .Where(userRole => !userRole.IsDeleted)
            .Join(
                _dbContext.Roles.AcrossTenants().Where(role => !role.IsDeleted),
                userRole => userRole.RoleId,
                role => role.Id,
                (userRole, role) => new { userRole.UserId, RoleName = role.Name, RoleTenantId = role.TenantId })
            .Join(
                _dbContext.Users.AcrossTenants().Where(user => !user.IsDeleted),
                item => item.UserId,
                user => user.Id,
                (item, user) => new { item.UserId, item.RoleName, item.RoleTenantId, user.Status, UserTenantId = user.TenantId })
            .Where(item => item.RoleTenantId == @event.TenantId
                && item.UserTenantId == @event.TenantId
                && item.RoleName == TenantAdminRoleName
                && item.Status == ActiveUserStatus)
            .Select(item => item.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (recipients.Count == 0)
        {
            _logger.LogWarning(
                "Low-stock alert {AlertId} for ingredient {IngredientId} has no active TenantAdmin recipients in tenant {TenantId}.",
                @event.AlertId, @event.IngredientId, @event.TenantId);
            return;
        }

        // Idempotency: the outbox dispatcher may re-deliver the same event after a crash-recovery
        // retry. Every delivery derives the SAME key from the alert id, so CreateForUserAsync's
        // unique (tenant, user, key) index yields exactly one notification per admin per alert.
        // CorrelationId is the alert id so refresh-cycle follow-ups can group later.
        foreach (var userId in recipients)
        {
            await _notificationService.CreateForUserAsync(
                new CreateNotificationRequest(
                    TenantId: @event.TenantId,
                    UserId: userId,
                    Type: "commerce.low_stock",
                    Source: "Commerce",
                    Title: $"Low stock: {@event.IngredientName}",
                    Body: $"{@event.IngredientName}: {FormatQuantity(@event.Available)} {@event.BaseUnit} available, reorder at {FormatQuantity(@event.ReorderPoint)} {@event.BaseUnit}.",
                    Severity: NotificationSeverities.Warning,
                    ActionUrl: null,
                    CorrelationId: @event.AlertId.ToString(),
                    AiRunId: null,
                    IdempotencyKey: $"low-stock-{@event.AlertId}"),
                cancellationToken).ConfigureAwait(false);
        }
    }

    private static string FormatQuantity(decimal value) => value.ToString("0.####");
}
