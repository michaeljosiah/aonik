using Aonik.SharedKernel.Abstractions.Connectors;

namespace Aonik.Agents.Services.Connectors.Catalog;

internal sealed class ConnectorPackageRegistration : IConnectorDescriptor
{
    public ConnectorDescriptor Descriptor { get; }
    public IReadOnlyDictionary<ConnectorActionRef, ConnectorPackageActionContract> Contracts { get; }

    public ConnectorPackageRegistration(IEnumerable<VerifiedConnectorPackage> releases)
    {
        VerifiedConnectorPackage[] packages = releases.ToArray();
        var manifest = packages[0].Manifest;
        Contracts = packages.SelectMany(package => package.Manifest.Actions.Select(action => (action, package.Manifest.Limits)))
            .DistinctBy(item => (item.action.Key, item.action.Version))
            .ToDictionary(item => new ConnectorActionRef(item.action.Key, item.action.Version),
                item => new ConnectorPackageActionContract(manifest.ConnectorKey, item.action, item.Limits));
        Descriptor = new(manifest.ConnectorKey, manifest.DisplayName, Contracts.Values.Select(contract => contract.Descriptor).ToArray(), "ServiceAccount");
    }
}
