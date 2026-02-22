using Aonik.SharedKernel.Events;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.SharedKernel.Tests.Events;

public class EventBusRegistrationTests
{
    public record SampleEvent(string Data) : IIntegrationEvent;

    public class SampleEventHandler : IEventHandler<SampleEvent>
    {
        public List<SampleEvent> ReceivedEvents { get; } = new();

        public Task HandleAsync(SampleEvent @event, CancellationToken cancellationToken = default)
        {
            ReceivedEvents.Add(@event);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public void AddEventBus_ShouldRegisterEventBus()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddEventBus();

        // Assert
        using var provider = services.BuildServiceProvider();
        var bus = provider.GetService<IEventBus>();
        bus.Should().NotBeNull();
        bus.Should().BeOfType<InProcessEventBus>();
    }

    [Fact]
    public void AddEventBus_ShouldRegisterBusAsScoped()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventBus();

        // Assert
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IEventBus));
        descriptor.Should().NotBeNull();
        descriptor!.Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddEventBus_ShouldScanAssemblyForHandlers()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act — scan the test assembly which contains SampleEventHandler
        services.AddEventBus(typeof(EventBusRegistrationTests).Assembly);

        // Assert
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var handlers = scope.ServiceProvider.GetServices<IEventHandler<SampleEvent>>().ToList();
        handlers.Should().Contain(h => h.GetType() == typeof(SampleEventHandler));
    }

    [Fact]
    public void AddEventHandlersFromAssembly_ShouldRegisterAdditionalHandlers()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventBus(); // no assemblies scanned initially

        // Act
        services.AddEventHandlersFromAssembly(typeof(EventBusRegistrationTests).Assembly);

        // Assert
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var handlers = scope.ServiceProvider.GetServices<IEventHandler<SampleEvent>>().ToList();
        handlers.Should().Contain(h => h.GetType() == typeof(SampleEventHandler));
    }

    [Fact]
    public async Task AddEventBus_ShouldWorkEndToEnd_WithScannedHandlers()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventBus(typeof(EventBusRegistrationTests).Assembly);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IEventBus>();

        // Act
        await bus.PublishAsync(new SampleEvent("end-to-end"));

        // Assert — handler was invoked (can verify via resolved handler instance)
        var handlers = scope.ServiceProvider.GetServices<IEventHandler<SampleEvent>>().ToList();
        handlers.OfType<SampleEventHandler>().Should().ContainSingle()
            .Which.ReceivedEvents.Should().ContainSingle()
            .Which.Data.Should().Be("end-to-end");
    }
}
