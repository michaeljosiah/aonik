using Aonik.SharedKernel.Events;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.SharedKernel.Tests.Events;

public class InProcessEventBusTests
{
    #region Test Events and Handlers

    public record TestEvent(string Message) : IIntegrationEvent;

    public record AnotherEvent(int Value) : IIntegrationEvent;

    public class TestEventHandler : IEventHandler<TestEvent>
    {
        public List<TestEvent> ReceivedEvents { get; } = new();

        public Task HandleAsync(TestEvent @event, CancellationToken cancellationToken = default)
        {
            ReceivedEvents.Add(@event);
            return Task.CompletedTask;
        }
    }

    public class SecondTestEventHandler : IEventHandler<TestEvent>
    {
        public List<TestEvent> ReceivedEvents { get; } = new();

        public Task HandleAsync(TestEvent @event, CancellationToken cancellationToken = default)
        {
            ReceivedEvents.Add(@event);
            return Task.CompletedTask;
        }
    }

    public class AnotherEventHandler : IEventHandler<AnotherEvent>
    {
        public List<AnotherEvent> ReceivedEvents { get; } = new();

        public Task HandleAsync(AnotherEvent @event, CancellationToken cancellationToken = default)
        {
            ReceivedEvents.Add(@event);
            return Task.CompletedTask;
        }
    }

    public class FailingHandler : IEventHandler<TestEvent>
    {
        public Task HandleAsync(TestEvent @event, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Handler failed deliberately");
        }
    }

    #endregion

    private static ServiceProvider BuildProvider(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        configure(services);
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task PublishAsync_ShouldInvokeRegisteredHandler()
    {
        // Arrange
        using var provider = BuildProvider(services =>
        {
            services.AddScoped<IEventBus, InProcessEventBus>();
            services.AddScoped<IEventHandler<TestEvent>, TestEventHandler>();
        });
        using var scope = provider.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IEventBus>();
        var handler = (TestEventHandler)scope.ServiceProvider.GetRequiredService<IEventHandler<TestEvent>>();
        var @event = new TestEvent("hello");

        // Act
        await bus.PublishAsync(@event);

        // Assert
        handler.ReceivedEvents.Should().ContainSingle()
            .Which.Message.Should().Be("hello");
    }

    [Fact]
    public async Task PublishAsync_ShouldInvokeMultipleHandlersForSameEvent()
    {
        // Arrange
        using var provider = BuildProvider(services =>
        {
            services.AddScoped<IEventBus, InProcessEventBus>();
            services.AddScoped<IEventHandler<TestEvent>, TestEventHandler>();
            services.AddScoped<IEventHandler<TestEvent>, SecondTestEventHandler>();
        });
        using var scope = provider.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IEventBus>();
        var handlers = scope.ServiceProvider.GetServices<IEventHandler<TestEvent>>().ToList();
        var @event = new TestEvent("multi");

        // Act
        await bus.PublishAsync(@event);

        // Assert
        handlers.Should().HaveCount(2);
        handlers.OfType<TestEventHandler>().Single().ReceivedEvents.Should().ContainSingle();
        handlers.OfType<SecondTestEventHandler>().Single().ReceivedEvents.Should().ContainSingle();
    }

    [Fact]
    public async Task PublishAsync_ShouldNotInvokeHandlersForDifferentEvent()
    {
        // Arrange
        using var provider = BuildProvider(services =>
        {
            services.AddScoped<IEventBus, InProcessEventBus>();
            services.AddScoped<IEventHandler<TestEvent>, TestEventHandler>();
            services.AddScoped<IEventHandler<AnotherEvent>, AnotherEventHandler>();
        });
        using var scope = provider.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IEventBus>();
        var testHandler = (TestEventHandler)scope.ServiceProvider.GetRequiredService<IEventHandler<TestEvent>>();
        var anotherHandler = (AnotherEventHandler)scope.ServiceProvider.GetRequiredService<IEventHandler<AnotherEvent>>();

        // Act
        await bus.PublishAsync(new AnotherEvent(42));

        // Assert
        anotherHandler.ReceivedEvents.Should().ContainSingle().Which.Value.Should().Be(42);
        testHandler.ReceivedEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task PublishAsync_ShouldCompleteSuccessfully_WhenNoHandlersRegistered()
    {
        // Arrange
        using var provider = BuildProvider(services =>
        {
            services.AddScoped<IEventBus, InProcessEventBus>();
        });
        using var scope = provider.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IEventBus>();

        // Act
        var act = () => bus.PublishAsync(new TestEvent("orphan"));

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task PublishAsync_ShouldPropagateHandlerException()
    {
        // Arrange
        using var provider = BuildProvider(services =>
        {
            services.AddScoped<IEventBus, InProcessEventBus>();
            services.AddScoped<IEventHandler<TestEvent>, FailingHandler>();
        });
        using var scope = provider.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IEventBus>();

        // Act
        var act = () => bus.PublishAsync(new TestEvent("fail"));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Handler failed deliberately");
    }
}
