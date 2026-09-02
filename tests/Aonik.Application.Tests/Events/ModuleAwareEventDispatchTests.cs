using Aonik.Commerce.IntegrationEvents;
using Aonik.Commerce.Services.Checkout;
using Aonik.Platform.Services.Modules;
using Aonik.SharedKernel.Events;
using Aonik.SharedKernel.Events.Integration;
using Aonik.SharedKernel.Modules;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Aonik.Application.Tests.Events;

/// <summary>
/// Spec 097 §12.3: the in-process bus skips a handler whose assembly module is disabled for the
/// event's tenant. Handlers outside a gated module always run, and a host without the reader
/// registered behaves exactly as before.
/// </summary>
public class ModuleAwareEventDispatchTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    [Fact]
    public async Task PublishAsync_Should_SkipHandlerFromDisabledModule_When_EventIsTenantScoped()
    {
        // Arrange
        var checkout = new Mock<ICheckoutService>();
        var recording = new RecordingHandler();
        var bus = CreateBus(checkout.Object, recording, new FakeReader(disabled: ModuleIds.Commerce));
        var @event = new PaymentCompletedEvent(TenantId, Guid.NewGuid(), Guid.NewGuid(), 10m, "GBP");

        // Act
        await bus.PublishAsync(@event);

        // Assert
        checkout.Verify(
            c => c.ConfirmPaymentAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "the Commerce handler must not run for a tenant with Commerce off");
        recording.Received.Should().ContainSingle().Which.Should().BeSameAs(@event);
    }

    [Fact]
    public async Task PublishAsync_Should_RunHandlerFromModule_When_ModuleIsEnabled()
    {
        // Arrange
        var checkout = new Mock<ICheckoutService>();
        var recording = new RecordingHandler();
        var bus = CreateBus(checkout.Object, recording, new FakeReader(disabled: ModuleIds.Workspaces));
        var orderId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var @event = new PaymentCompletedEvent(TenantId, paymentId, orderId, 10m, "GBP");

        // Act
        await bus.PublishAsync(@event);

        // Assert
        checkout.Verify(c => c.ConfirmPaymentAsync(orderId, paymentId, It.IsAny<CancellationToken>()), Times.Once);
        recording.Received.Should().ContainSingle();
    }

    [Fact]
    public async Task PublishAsync_Should_RunEveryHandler_When_NoReaderIsRegistered()
    {
        // Arrange
        var checkout = new Mock<ICheckoutService>();
        var recording = new RecordingHandler();
        var bus = CreateBus(checkout.Object, recording, reader: null);
        var orderId = Guid.NewGuid();
        var @event = new PaymentCompletedEvent(TenantId, Guid.NewGuid(), orderId, 10m, "GBP");

        // Act
        await bus.PublishAsync(@event);

        // Assert
        checkout.Verify(c => c.ConfirmPaymentAsync(orderId, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Once);
        recording.Received.Should().ContainSingle();
    }

    [Fact]
    public async Task PublishAsync_Should_NotConsultReader_When_EventCarriesNoTenant()
    {
        // Arrange
        var reader = new FakeReader(disabled: ModuleIds.Commerce);
        var services = new ServiceCollection();
        var recording = new RecordingHandler<UntenantedEvent>();
        services.AddSingleton<IEventHandler<UntenantedEvent>>(recording);
        services.AddSingleton<IModuleEnablementReader>(reader);
        var bus = new InProcessEventBus(services.BuildServiceProvider(), NullLogger<InProcessEventBus>.Instance);

        // Act
        await bus.PublishAsync(new UntenantedEvent());

        // Assert
        recording.Received.Should().ContainSingle();
        reader.Calls.Should().Be(0);
    }

    [Fact]
    public void GatedModuleId_Should_ReturnModule_When_HandlerLivesInNonCoreModuleAssembly()
    {
        TenantScopedHandlerGate.GatedModuleId(typeof(CommercePaymentCompletedHandler)).Should().Be(ModuleIds.Commerce);
    }

    [Fact]
    public void GatedModuleId_Should_ReturnNull_When_HandlerLivesInCoreModuleAssembly()
    {
        TenantScopedHandlerGate.GatedModuleId(typeof(TenantModulesChangedCacheInvalidator)).Should().BeNull();
    }

    [Fact]
    public void GatedModuleId_Should_ReturnNull_When_HandlerAssemblyHasNoModuleAttribute()
    {
        TenantScopedHandlerGate.GatedModuleId(typeof(RecordingHandler)).Should().BeNull();
    }

    [Fact]
    public async Task TryResolveAsync_Should_ReturnNull_When_TenantIsEmpty()
    {
        // Arrange
        var reader = new FakeReader();
        var services = new ServiceCollection();
        services.AddSingleton<IModuleEnablementReader>(reader);
        var @event = new PaymentCompletedEvent(Guid.Empty, Guid.NewGuid(), null, 1m, "GBP");

        // Act
        var enablement = await TenantScopedHandlerGate.TryResolveAsync(services.BuildServiceProvider(), @event);

        // Assert
        enablement.Should().BeNull();
        reader.Calls.Should().Be(0);
    }

    private static InProcessEventBus CreateBus(ICheckoutService checkout, RecordingHandler recording, IModuleEnablementReader? reader)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IEventHandler<PaymentCompletedEvent>>(
            new CommercePaymentCompletedHandler(checkout, NullLogger<CommercePaymentCompletedHandler>.Instance));
        services.AddSingleton<IEventHandler<PaymentCompletedEvent>>(recording);
        if (reader is not null)
            services.AddSingleton(reader);

        return new InProcessEventBus(services.BuildServiceProvider(), NullLogger<InProcessEventBus>.Instance);
    }

    private sealed record UntenantedEvent : IIntegrationEvent;

    private sealed class RecordingHandler : RecordingHandler<PaymentCompletedEvent>;

    private class RecordingHandler<TEvent> : IEventHandler<TEvent> where TEvent : IIntegrationEvent
    {
        public List<TEvent> Received { get; } = [];

        public Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default)
        {
            Received.Add(@event);
            return Task.CompletedTask;
        }
    }

    internal sealed class FakeReader(params string[] disabled) : IModuleEnablementReader
    {
        public int Calls { get; private set; }

        public Task<ModuleEnablementSet> GetAsync(Guid tenantId, CancellationToken ct = default)
        {
            Calls++;
            var enabled = ModuleCatalog.All.Select(m => m.Id).Except(disabled, StringComparer.Ordinal)
                .ToHashSet(StringComparer.Ordinal);
            return Task.FromResult(new ModuleEnablementSet(tenantId, enabled));
        }

        public Task<IReadOnlyList<Guid>> FilterEnabledTenantsAsync(
            IEnumerable<Guid> tenantIds, string moduleId, CancellationToken ct = default)
        {
            Calls++;
            IReadOnlyList<Guid> result = disabled.Contains(moduleId, StringComparer.Ordinal)
                ? []
                : tenantIds.Distinct().ToList();
            return Task.FromResult(result);
        }
    }
}
