using Aonik.Platform.Entities.Notifications;
using Aonik.Platform.Notifications;
using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aonik.Platform.Services.Seeding;

/// <summary>
/// Platform module's demo-seed contributor. Currently owns the
/// notification seed for a fresh demo install — populates the bell
/// badge with five plausible items so the notifications surface isn't
/// empty after the seed completes.
/// </summary>
internal sealed class PlatformDemoSeedContributor : IDemoSeedContributor
{
    private readonly PlatformDbContext _dbContext;
    private readonly ILogger<PlatformDemoSeedContributor> _logger;
    private readonly Dictionary<string, object> _results = new();

    public PlatformDemoSeedContributor(
        PlatformDbContext dbContext,
        ILogger<PlatformDemoSeedContributor> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public string ModuleName => "Platform";

    public async Task<IReadOnlyList<string>> SeedAsync(
        DemoSeedPhase phase,
        DemoSeedContext context,
        CancellationToken cancellationToken = default)
    {
        return phase switch
        {
            DemoSeedPhase.Activity => await SeedNotificationsAsync(context, cancellationToken),
            _ => Array.Empty<string>(),
        };
    }

    public void ClearTracking() => _dbContext.ChangeTracker.Clear();

    public IReadOnlyDictionary<string, object> GetResults() => _results;

    private async Task<IReadOnlyList<string>> SeedNotificationsAsync(
        DemoSeedContext context,
        CancellationToken cancellationToken)
    {
        // Notifications are user-scoped — without a current user there's
        // no inbox to seed. Bail rather than orphaning rows.
        if (!context.UserId.HasValue)
        {
            return Array.Empty<string>();
        }

        var userId = context.UserId.Value;
        var now = context.Now;

        // Hard-delete prior demo notifications for this user. Plain
        // RemoveRange would be soft-deleted by the audit hook, leaving
        // ghost rows that re-seeds would leak into the bell counter.
        await _dbContext.Notifications
            .IgnoreQueryFilters()
            .Where(n => n.TenantId == context.TenantId && n.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        var seeds = new[]
        {
            new NotificationSeed(
                "Proposal pending review",
                "Billing Agent proposed matching INV-2041 (£12,480) to bank txn 9f2c1a. Confidence 0.94.",
                NotificationSeverities.Info,
                "agent.proposal.pending",
                "BillingAgent",
                "/approvals",
                now.AddMinutes(-12),
                NotificationStatuses.Unread),

            new NotificationSeed(
                "Sanctions screening flagged",
                "Naledi Dlamini moved above 0.6 risk score after the latest UK sanctions list refresh.",
                NotificationSeverities.Warning,
                "compliance.case.opened",
                "ComplianceAgent",
                "/compliance",
                now.AddHours(-2),
                NotificationStatuses.Unread),

            new NotificationSeed(
                "Spend anomaly detected",
                "Fuel category up 47% on the 30-day rolling average — driver fleet running weekend trips.",
                NotificationSeverities.Info,
                "insights.anomaly.detected",
                "InsightsAgent",
                "/",
                now.AddDays(-1),
                NotificationStatuses.Unread),

            new NotificationSeed(
                "Order completed",
                "Acme Imports → Peter Mwangi: £2,500 transfer settled at ₦4,902,500.",
                NotificationSeverities.Success,
                "order.complete",
                "Orders",
                "/orders",
                now.AddDays(-5),
                NotificationStatuses.Read),

            new NotificationSeed(
                "Welcome to Aonik",
                "Demo data has been seeded. Explore /ai/workflows for the agent registry and /orders for sample activity.",
                NotificationSeverities.Info,
                "system.welcome",
                "System",
                "/",
                now.AddDays(-7),
                NotificationStatuses.Read),
        };

        var notificationIds = new List<Guid>();
        foreach (var seed in seeds)
        {
            var notification = new Notification
            {
                Id = Guid.NewGuid(),
                TenantId = context.TenantId,
                UserId = userId,
                Channel = NotificationChannels.InApp,
                Type = seed.Type,
                Source = seed.Source,
                Title = seed.Title,
                Body = seed.Body,
                Severity = seed.Severity,
                Status = seed.Status,
                ActionUrl = seed.ActionUrl,
                ReadAt = seed.Status == NotificationStatuses.Read ? seed.CreatedAt.AddMinutes(5) : null,
                MetadataJson = "{}",
                CreatedAt = seed.CreatedAt,
                CreatedBy = userId,
            };
            _dbContext.Notifications.Add(notification);
            notificationIds.Add(notification.Id);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        _results[DemoSeedResultKeys.NotificationIds] = notificationIds.ToArray();
        return new[] { $"Seeded {notificationIds.Count} notifications" };
    }

    private sealed record NotificationSeed(
        string Title,
        string Body,
        string Severity,
        string Type,
        string Source,
        string ActionUrl,
        DateTime CreatedAt,
        string Status);
}
