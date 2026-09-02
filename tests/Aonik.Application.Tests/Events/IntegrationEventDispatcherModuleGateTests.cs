using System.Text.Json;
using Aonik.Commerce.IntegrationEvents;
using Aonik.Commerce.Services.Checkout;
using Aonik.Infrastructure.Messaging.Outbox;
using Aonik.Infrastructure.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Events;
using Aonik.SharedKernel.Events.Integration;
using Aonik.SharedKernel.Events.Outbox;
using Aonik.SharedKernel.Modules;
using Aonik.TestSupport.Multitenancy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Aonik.Application.Tests.Events;

/// <summary>
/// Spec 097 §12.3 for the outbox path: the dispatcher skips a handler whose module is disabled for
/// the event's tenant and records the inbox row anyway, so the skip is a completed delivery rather
/// than something retried forever. Lives here rather than in Aonik.Infrastructure.Tests because the
/// only handlers in non-core module assemblies are internal, and Commerce exposes its internals to
/// this project only.
/// </summary>
public class IntegrationEventDispatcherModuleGateTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 9, 2, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task DispatchAsync_Should_SkipHandlerAndRecordInbox_When_ModuleIsDisabledForTenant()
    {
        // Arrange
        var checkout = new Mock<ICheckoutService>();
        var recording = new RecordingHandler();
        using var dbContext = CreateDbContext();
        var dispatcher = CreateDispatcher(dbContext, checkout.Object, recording,
            new ModuleAwareEventDispatchTests.FakeReader(disabled: ModuleIds.Commerce));
        var orderId = Guid.NewGuid();
        var message = CreateMessage(new PaymentCompletedEvent(TenantId, Guid.NewGuid(), orderId, 10m, "GBP"));

        // Act
        await dispatcher.DispatchAsync(message);

        // Assert
        checkout.Verify(
            c => c.ConfirmPaymentAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        recording.Received.Should().ContainSingle();

        var inbox = dbContext.ChangeTracker.Entries<InboxMessage>().Select(e => e.Entity).ToList();
        inbox.Should().HaveCount(2, "both the skipped and the executed handler count as delivered");
        inbox.Should().ContainSingle(x =>
            x.EventId == message.EventId
            && x.HandlerName == typeof(CommercePaymentCompletedHandler).FullName
            && x.ProcessedAt == Now);
        inbox.Should().ContainSingle(x =>
            x.EventId == message.EventId && x.HandlerName == typeof(RecordingHandler).FullName);
    }

    [Fact]
    public async Task DispatchAsync_Should_RunHandler_When_ModuleIsEnabledForTenant()
    {
        // Arrange
        var checkout = new Mock<ICheckoutService>();
        var recording = new RecordingHandler();
        using var dbContext = CreateDbContext();
        var dispatcher = CreateDispatcher(dbContext, checkout.Object, recording,
            new ModuleAwareEventDispatchTests.FakeReader(disabled: ModuleIds.Workspaces));
        var orderId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var message = CreateMessage(new PaymentCompletedEvent(TenantId, paymentId, orderId, 10m, "GBP"));

        // Act
        await dispatcher.DispatchAsync(message);

        // Assert
        checkout.Verify(c => c.ConfirmPaymentAsync(orderId, paymentId, It.IsAny<CancellationToken>()), Times.Once);
        recording.Received.Should().ContainSingle();
        dbContext.ChangeTracker.Entries<InboxMessage>().Should().HaveCount(2);
    }

    [Fact]
    public async Task DispatchAsync_Should_RunEveryHandler_When_NoReaderIsRegistered()
    {
        // Arrange
        var checkout = new Mock<ICheckoutService>();
        var recording = new RecordingHandler();
        using var dbContext = CreateDbContext();
        var dispatcher = CreateDispatcher(dbContext, checkout.Object, recording, reader: null);
        var orderId = Guid.NewGuid();
        var message = CreateMessage(new PaymentCompletedEvent(TenantId, Guid.NewGuid(), orderId, 10m, "GBP"));

        // Act
        await dispatcher.DispatchAsync(message);

        // Assert
        checkout.Verify(c => c.ConfirmPaymentAsync(orderId, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Once);
        recording.Received.Should().ContainSingle();
    }

    private static AonikDbContext CreateDbContext()
    {
        // EF Core caches one model per context type per internal service provider, and every other
        // InMemory AonikDbContext in this test process shares the default provider. A context built
        // here WITHOUT a tenant provider would bake a filter-less model into that shared cache
        // (AonikDbContextBase.ApplyTenantQueryFilters returns early on a null provider) and silently
        // strip the tenant filters from every later context — which made the fail-closed
        // UserAssociationTenantResolutionFailClosedTests order-dependent. So: a real tenant provider,
        // and a private internal service provider so this model never reaches the shared cache.
        var internalServices = new ServiceCollection()
            .AddEntityFrameworkInMemoryDatabase()
            .BuildServiceProvider();

        var options = new DbContextOptionsBuilder<AonikDbContext>()
            .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
            .UseInternalServiceProvider(internalServices)
            .Options;
        return new AonikDbContext(options, new TestTenantProvider(TenantId));
    }

    private static IntegrationEventDispatcher CreateDispatcher(
        AonikDbContext dbContext,
        ICheckoutService checkout,
        RecordingHandler recording,
        IModuleEnablementReader? reader)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IEventHandler<PaymentCompletedEvent>>(
            new CommercePaymentCompletedHandler(checkout, NullLogger<CommercePaymentCompletedHandler>.Instance));
        services.AddSingleton<IEventHandler<PaymentCompletedEvent>>(recording);
        if (reader is not null)
            services.AddSingleton(reader);

        return new IntegrationEventDispatcher(
            dbContext,
            services.BuildServiceProvider(),
            new IntegrationEventTypeRegistry([]),
            new FixedClock(Now),
            NullLogger<IntegrationEventDispatcher>.Instance);
    }

    private static OutboxMessage CreateMessage(PaymentCompletedEvent @event) => new()
    {
        EventId = Guid.NewGuid(),
        EventType = typeof(PaymentCompletedEvent).FullName!,
        Payload = JsonSerializer.Serialize(@event, OutboxSerialization.Options),
        TenantId = @event.TenantId,
        OccurredAt = Now,
        CreatedAt = Now,
    };

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow => utcNow;
    }

    private sealed class RecordingHandler : IEventHandler<PaymentCompletedEvent>
    {
        public List<PaymentCompletedEvent> Received { get; } = [];

        public Task HandleAsync(PaymentCompletedEvent @event, CancellationToken cancellationToken = default)
        {
            Received.Add(@event);
            return Task.CompletedTask;
        }
    }
}
