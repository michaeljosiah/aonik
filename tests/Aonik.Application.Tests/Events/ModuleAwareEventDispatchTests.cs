using Aonik.Commerce.IntegrationEvents;
using Aonik.Commerce.Services.Checkout;
using Aonik.SharedKernel.Events;
using Aonik.SharedKernel.Events.Integration;
using Aonik.SharedKernel.Modules;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Aonik.Application.Tests.Events;

/// <summary>
/// Spec 097 §12.3: integration event handlers are deliberately NOT module-gated. An event announces
/// work the publisher already committed, so the handler that reacts to it keeps its module's data
/// consistent with what happened — including the ledger postings a usage drawdown owes. Refusing to
/// run it would corrupt that state rather than protect it, and for a money-touching handler the loss
/// would be permanent (the outbox records the delivery either way). New activity is refused at the
/// entry points instead: HTTP, agents, jobs and proposals.
/// </summary>
public class ModuleAwareEventDispatchTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    [Fact]
    public async Task PublishAsync_Should_RunTheHandler_When_ItsModuleIsDisabledForTheTenant()
    {
        // Arrange — Commerce is off for this tenant, but the payment it is being told about already
        // completed, so the handler must still reconcile the order it belongs to.
        var checkout = new Mock<ICheckoutService>();
        var recording = new RecordingHandler();
        var reader = new FakeReader(disabled: ModuleIds.Commerce);
        var bus = CreateBus(checkout.Object, recording, reader);
        var orderId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var @event = new PaymentCompletedEvent(TenantId, paymentId, orderId, 10m, "GBP");

        // Act
        await bus.PublishAsync(@event);

        // Assert
        checkout.Verify(
            c => c.ConfirmPaymentAsync(orderId, paymentId, It.IsAny<CancellationToken>()),
            Times.Once,
            "committed work must be reconciled even while the module is switched off");
        recording.Received.Should().ContainSingle().Which.Should().BeSameAs(@event);
        reader.Calls.Should().Be(0, "delivery never depends on module state, so the reader is not consulted");
    }

    [Fact]
    public async Task PublishAsync_Should_RunEveryHandler_When_ModuleIsEnabled()
    {
        // Arrange
        var checkout = new Mock<ICheckoutService>();
        var recording = new RecordingHandler();
        var bus = CreateBus(checkout.Object, recording, new FakeReader());
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
    public async Task PublishAsync_Should_RunTheHandler_When_EventCarriesNoTenant()
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
