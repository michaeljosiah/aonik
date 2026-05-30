using System.Reflection;
using Aonik.SharedKernel.Events;

namespace Aonik.Infrastructure.Messaging.Outbox;

/// <summary>
/// Builds a <see cref="System.Type.FullName"/> → <see cref="System.Type"/> map of
/// every concrete <see cref="IIntegrationEvent"/> found in the SharedKernel
/// assembly (where the canonical events live) plus any additional assemblies
/// supplied at registration. The map is immutable after construction, so lookups
/// are lock-free.
/// </summary>
public sealed class IntegrationEventTypeRegistry : IIntegrationEventTypeRegistry
{
    private readonly Dictionary<string, Type> _typesByName;

    public IntegrationEventTypeRegistry(IEnumerable<Assembly> assemblies)
    {
        _typesByName = new Dictionary<string, Type>(StringComparer.Ordinal);

        // SharedKernel always carries the canonical events; callers may add module
        // assemblies that define their own. Dedup so one assembly is scanned once.
        var scanned = new HashSet<Assembly> { typeof(IIntegrationEvent).Assembly };
        foreach (var assembly in assemblies)
        {
            scanned.Add(assembly);
        }

        foreach (var assembly in scanned)
        {
            foreach (var type in GetLoadableTypes(assembly))
            {
                if (type is { IsAbstract: false, IsInterface: false }
                    && typeof(IIntegrationEvent).IsAssignableFrom(type)
                    && type.FullName is { } fullName)
                {
                    // Last writer wins on a FullName collision; events are expected
                    // to be uniquely named across modules.
                    _typesByName[fullName] = type;
                }
            }
        }
    }

    public Type? Resolve(string eventTypeName) =>
        _typesByName.TryGetValue(eventTypeName, out var type) ? type : null;

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            // A failed-to-load type in an unrelated part of the assembly must not
            // sink the whole scan; keep the types that did load.
            return ex.Types.Where(t => t is not null)!;
        }
    }
}
