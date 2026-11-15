using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Aonik.SharedKernel.Abstractions.Connectors;

namespace Aonik.Agents.Services.Connectors.Catalog;

internal static class ConnectorPackageReferences
{
    public static string Serialize(VerifiedConnectorPackage package, ConnectorActionRef? action = null)
        => JsonSerializer.Serialize(new ConnectorPackageInstallationRef(package.Manifest.PackageKey, package.Manifest.PackageVersion, package.Digest, action), ConnectorPackageVerifier.JsonOptions);

    public static (VerifiedConnectorPackage Package, ConnectorPackageAction? Action) Require(CuratedConnectorCatalog catalog, string json, string adapterKind)
    {
        if (Encoding.UTF8.GetByteCount(json) > 2000) throw Invalid();
        ConnectorPackageInstallationRef reference;
        try { reference = JsonSerializer.Deserialize<ConnectorPackageInstallationRef>(json, ConnectorPackageVerifier.JsonOptions) ?? throw Invalid(); }
        catch (JsonException) { throw Invalid(); }
        if (reference.Digest is not { Length: 64 } || !reference.Digest.All(Uri.IsHexDigit)) throw Invalid();
        VerifiedConnectorPackage package = catalog.Require(reference.PackageKey, reference.PackageVersion, reference.Digest);
        if (package.Manifest.AdapterKind != adapterKind || (adapterKind == "Http") != (reference.Action is not null)) throw Invalid();
        ConnectorPackageAction? action = reference.Action is { } selected ? package.Manifest.Actions.SingleOrDefault(action => action.Key == selected.Key && action.Version == selected.Version)
            ?? throw Invalid() : null;
        return (package, action);
    }

    public static string ToolName(ConnectorPackageAction action)
        => "connector_" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(action.Key + "@" + action.Version.ToString(CultureInfo.InvariantCulture))))[..32];
    public static string ServerName(VerifiedConnectorPackage package) => "catalog_" + package.Manifest.PackageKey + "_" + package.Manifest.PackageVersion;
    public static string HttpToolName(VerifiedConnectorPackage package, ConnectorPackageAction action) => ServerName(package) + "_" + ToolName(action);
    private static InvalidOperationException Invalid() => new("ConnectorPackage.InstallationReferenceInvalid");
}
