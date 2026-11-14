using Aonik.SharedKernel.Abstractions.Connectors;

namespace Aonik.Platform.Services.Integrations;

internal sealed class ConnectorCatalog(IEnumerable<IConnectorDescriptor> registrations)
{
    private readonly IReadOnlyDictionary<string, ConnectorDescriptor> _connectors = registrations
        .ToDictionary(registration => registration.Descriptor.Key, registration => registration.Descriptor, StringComparer.Ordinal);
    public IReadOnlyList<ConnectorDescriptor> Descriptors => _connectors.Values.OrderBy(descriptor => descriptor.Key, StringComparer.Ordinal).ToList();
    public ConnectorDescriptor? Find(string key) => _connectors.GetValueOrDefault(key);
    public ConnectorActionDescriptor Resolve(string connectorKey, ConnectorActionRef action) => Find(connectorKey)?.Actions
        .SingleOrDefault(candidate => candidate.Key == action.Key && candidate.Version == action.Version)
        ?? throw new ConnectorAccessException("ConnectorUnavailable", "The reviewed connector action/version is unavailable.");
    public ConnectorActionDescriptor Resolve(ConnectorActionRef action) => Descriptors.SelectMany(connector => connector.Actions)
        .SingleOrDefault(candidate => candidate.Key == action.Key && candidate.Version == action.Version)
        ?? throw new ConnectorAccessException("ConnectorUnavailable", "The reviewed connector action/version is unavailable.");
}
