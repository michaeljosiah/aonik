using Aonik.Platform.Contracts.Models.Notifications;
using Aonik.Platform.Contracts.Services.Notifications;
using Aonik.Platform.Entities.Identity;
using Aonik.Platform.IntegrationEvents;
using Aonik.Platform.Notifications;
using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Events.Integration;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Aonik.Application.Tests.Notifications;

/// <summary>
/// The Platform-side bridge for Commerce low-stock alerts (Spec 052 §10): resolves the alert
/// tenant's active TenantAdmin users and posts one Spec 016 inbox notification each, keyed
/// <c>low-stock-{alertId}</c> so outbox re-delivery deduplicates.
/// </summary>
public sealed class LowStockAlertNotificationHandlerTests
{
    private readonly Guid _tenantId = Guid.NewGuid();

    private PlatformDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase($"PlatformLowStock_{Guid.NewGuid()}")
            .Options;
        return new PlatformDbContext(options, new TestTenantProvider(_tenantId));
    }

    private static Mock<INotificationService> CreateNotificationService()
    {
        var service = new Mock<INotificationService>();
        service
            .Setup(s => s.CreateForUserAsync(It.IsAny<CreateNotificationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CreateNotificationRequest r, CancellationToken _) => new NotificationResponse(
                Id: Guid.NewGuid(),
                TenantId: r.TenantId,
                UserId: r.UserId,
                Channel: r.Channel,
                Type: r.Type,
                Source: r.Source,
                Title: r.Title,
                Body: r.Body,
                Severity: r.Severity,
                Status: "Unread",
                ActionUrl: r.ActionUrl,
                CorrelationId: r.CorrelationId,
                AiRunId: r.AiRunId,
                MetadataJson: r.MetadataJson ?? "{}",
                CreatedAt: new DateTime(2026, 7, 1, 9, 0, 0, DateTimeKind.Utc),
                ReadAt: null,
                DismissedAt: null));
        return service;
    }

    private async Task<Guid> SeedUserWithRoleAsync(PlatformDbContext context, Guid roleTenantId, string roleName, string userStatus = "Active")
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            TenantId = roleTenantId,
            ExternalIssuer = "test",
            ExternalSubject = Guid.NewGuid().ToString(),
            Status = userStatus,
        };
        var role = new Role { Id = Guid.NewGuid(), TenantId = roleTenantId, Name = roleName };
        context.Users.Add(user);
        context.Roles.Add(role);
        context.UserRoles.Add(new UserRole { Id = Guid.NewGuid(), UserId = user.Id, RoleId = role.Id });
        await context.SaveChangesAsync();
        return user.Id;
    }

    private LowStockAlertRaisedEvent NewEvent(Guid? alertId = null) => new(
        TenantId: _tenantId,
        AlertId: alertId ?? Guid.NewGuid(),
        IngredientId: Guid.NewGuid(),
        IngredientName: "Rice",
        BaseUnit: "kg",
        Available: 2m,
        ReorderPoint: 5m);

    [Fact]
    public async Task HandleAsync_Should_NotifyEachActiveTenantAdmin_WithAlertKeyedIdempotency()
    {
        await using var context = CreateContext();
        var admin1 = await SeedUserWithRoleAsync(context, _tenantId, "TenantAdmin");
        var admin2 = await SeedUserWithRoleAsync(context, _tenantId, "TenantAdmin");
        await SeedUserWithRoleAsync(context, _tenantId, "Analyst");                     // not an admin
        await SeedUserWithRoleAsync(context, _tenantId, "TenantAdmin", "Suspended");    // not active
        var service = CreateNotificationService();
        var handler = new LowStockAlertNotificationHandler(
            context, service.Object, NullLogger<LowStockAlertNotificationHandler>.Instance);
        var @event = NewEvent();

        await handler.HandleAsync(@event);

        foreach (var admin in new[] { admin1, admin2 })
        {
            service.Verify(s => s.CreateForUserAsync(
                It.Is<CreateNotificationRequest>(r =>
                    r.TenantId == _tenantId
                    && r.UserId == admin
                    && r.Type == "commerce.low_stock"
                    && r.Source == "Commerce"
                    && r.Title == "Low stock: Rice"
                    && r.Body == "Rice: 2 kg available, reorder at 5 kg."
                    && r.Severity == NotificationSeverities.Warning
                    && r.CorrelationId == @event.AlertId.ToString()
                    && r.IdempotencyKey == $"low-stock-{@event.AlertId}"),
                It.IsAny<CancellationToken>()), Times.Once);
        }
        service.Verify(s => s.CreateForUserAsync(
            It.IsAny<CreateNotificationRequest>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task HandleAsync_Should_IgnoreAdminsOfOtherTenants()
    {
        await using var context = CreateContext();
        await SeedUserWithRoleAsync(context, Guid.NewGuid(), "TenantAdmin"); // someone else's admin
        var service = CreateNotificationService();
        var handler = new LowStockAlertNotificationHandler(
            context, service.Object, NullLogger<LowStockAlertNotificationHandler>.Instance);

        await handler.HandleAsync(NewEvent());

        service.Verify(s => s.CreateForUserAsync(
            It.IsAny<CreateNotificationRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Should_NotNotify_WhenAdminRoleAssignmentIsSoftDeleted()
    {
        // The recipient join reads AcrossTenants(), which drops the soft-delete filter too —
        // a revoked TenantAdmin assignment is a soft-deleted UserRole and must not be notified.
        await using var context = CreateContext();
        var revoked = await SeedUserWithRoleAsync(context, _tenantId, "TenantAdmin");
        var assignment = await context.UserRoles.SingleAsync(ur => ur.UserId == revoked);
        context.UserRoles.Remove(assignment); // soft delete (IsDeleted = true on save)
        await context.SaveChangesAsync();
        var service = CreateNotificationService();
        var handler = new LowStockAlertNotificationHandler(
            context, service.Object, NullLogger<LowStockAlertNotificationHandler>.Instance);

        await handler.HandleAsync(NewEvent());

        service.Verify(s => s.CreateForUserAsync(
            It.IsAny<CreateNotificationRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Should_NotNotify_WhenAdminUserIsSoftDeleted()
    {
        await using var context = CreateContext();
        var deleted = await SeedUserWithRoleAsync(context, _tenantId, "TenantAdmin");
        var user = await context.Users.SingleAsync(u => u.Id == deleted);
        context.Users.Remove(user); // soft delete (IsDeleted = true on save)
        await context.SaveChangesAsync();
        var service = CreateNotificationService();
        var handler = new LowStockAlertNotificationHandler(
            context, service.Object, NullLogger<LowStockAlertNotificationHandler>.Instance);

        await handler.HandleAsync(NewEvent());

        service.Verify(s => s.CreateForUserAsync(
            It.IsAny<CreateNotificationRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Should_NoOp_WhenTenantHasNoAdmins()
    {
        await using var context = CreateContext();
        var service = CreateNotificationService();
        var handler = new LowStockAlertNotificationHandler(
            context, service.Object, NullLogger<LowStockAlertNotificationHandler>.Instance);

        var act = async () => await handler.HandleAsync(NewEvent());

        await act.Should().NotThrowAsync();
        service.Verify(s => s.CreateForUserAsync(
            It.IsAny<CreateNotificationRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
