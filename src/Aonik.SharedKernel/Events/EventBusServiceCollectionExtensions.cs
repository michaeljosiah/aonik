using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.SharedKernel.Events;

/// <summary>
/// Extension methods for registering the event bus and scanning for event handlers.
/// </summary>
public static class EventBusServiceCollectionExtensions
{
    /// <summary>
    /// Registers the in-process event bus and scans the provided assemblies for
    /// <see cref="IEventHandler{TEvent}"/> implementations.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="assemblies">Assemblies to scan for event handler implementations.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddEventBus(this IServiceCollection services, params Assembly[] assemblies)
    {
        services.AddScoped<IEventBus, InProcessEventBus>();

        foreach (var assembly in assemblies)
        {
            RegisterHandlersFromAssembly(services, assembly);
        }

        return services;
    }

    /// <summary>
    /// Scans an additional assembly for event handlers and registers them.
    /// Use this when adding a module after the initial <see cref="AddEventBus"/> call.
    /// </summary>
    public static IServiceCollection AddEventHandlersFromAssembly(this IServiceCollection services, Assembly assembly)
    {
        RegisterHandlersFromAssembly(services, assembly);
        return services;
    }

    private static void RegisterHandlersFromAssembly(IServiceCollection services, Assembly assembly)
    {
        var handlerInterfaceType = typeof(IEventHandler<>);

        var handlerRegistrations = assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false })
            .SelectMany(t => t.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == handlerInterfaceType)
                .Select(i => new { InterfaceType = i, ImplementationType = t }));

        foreach (var registration in handlerRegistrations)
        {
            services.AddScoped(registration.InterfaceType, registration.ImplementationType);
        }
    }
}
