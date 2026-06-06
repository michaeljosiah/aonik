using Aonik.SharedKernel.Abstractions.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Platform.Services.Tasks;

/// <summary>
/// <see cref="ITaskActionHandlerCatalog"/> backed by the DI container's keyed-service
/// registry. <see cref="IServiceProviderIsKeyedService"/> reports whether a key is
/// registered without instantiating the (scoped) handler — cheap and side-effect free.
/// </summary>
internal sealed class TaskActionHandlerCatalog : ITaskActionHandlerCatalog
{
    private readonly IServiceProviderIsKeyedService _keyedServiceProbe;

    public TaskActionHandlerCatalog(IServiceProviderIsKeyedService keyedServiceProbe)
    {
        _keyedServiceProbe = keyedServiceProbe;
    }

    public bool IsRegistered(string actionType) =>
        !string.IsNullOrWhiteSpace(actionType)
        && _keyedServiceProbe.IsKeyedService(typeof(ITaskActionHandler), actionType);
}
