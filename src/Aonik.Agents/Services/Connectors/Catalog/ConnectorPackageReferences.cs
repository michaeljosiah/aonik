using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Aonik.SharedKernel.Abstractions.Connectors;
using Aonik.SharedKernel.Serialization;
using Aonik.Agents.Entities;

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

    public static VerifiedConnectorPackage Require(CuratedConnectorCatalog catalog, TenantMcpServer server, Guid tenantId)
    {
        var (package, _) = Require(catalog, server.ConnectorPackageJson ?? throw Invalid(), "Mcp");
        if (server.TenantId != tenantId || server.Name != ServerName(package) || server.Endpoint != package.Manifest.Actions[0].Endpoint
            || server.TransportType != TenantMcpTransportType.Http || server.AuthKind.ToString() != package.Manifest.Authentication.Kind
            || !(JsonSerializer.Deserialize<string[]>(server.AllowedToolPrefixesJson) ?? []).Order(StringComparer.Ordinal)
                .SequenceEqual(package.Manifest.Actions.Select(action => action.RemoteToolName!).Order(StringComparer.Ordinal))) throw Invalid();
        return package;
    }

    public static (VerifiedConnectorPackage Package, ConnectorPackageAction Action) Require(CuratedConnectorCatalog catalog, TenantHttpTool tool, Guid tenantId)
    {
        var (package, action) = Require(catalog, tool.ConnectorPackageJson ?? throw Invalid(), "Http");
        if (tool.TenantId != tenantId || tool.Name != HttpToolName(package, action!) || tool.Description != action!.DisplayName
            || tool.Method != action.HttpMethod || tool.UrlTemplate != action.Endpoint || tool.AuthKind.ToString() != package.Manifest.Authentication.Kind
            || CanonicalJson.Hash(JsonSerializer.Deserialize<JsonElement>(tool.ParameterSchemaJson, ConnectorPackageVerifier.JsonOptions)) != CanonicalJson.Hash(action.InputSchema)) throw Invalid();
        return (package, action);
    }
    private static InvalidOperationException Invalid() => new("ConnectorPackage.InstallationReferenceInvalid");
}
