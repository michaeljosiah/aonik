using System.Text;

using Aonik.SharedKernel.Abstractions.Connectors;
using Aonik.SharedKernel.Serialization;

namespace Aonik.Agents.Services.Connectors.Catalog;

/// <summary>Deployment-curated passive artifacts, verified before composition. Tenant requests cannot supply paths or publisher keys.</summary>
internal sealed class CuratedConnectorCatalog
{
    public IReadOnlyList<VerifiedConnectorPackage> Packages { get; }

    public CuratedConnectorCatalog(ConnectorPackageTrustOptions options)
    {
        if (options.ArtifactFiles.Count > 100 || options.ArtifactFiles.Distinct(StringComparer.Ordinal).Count() != options.ArtifactFiles.Count)
            throw Invalid("CatalogLimit");
        if (options.ArtifactFiles.Count == 0) { Packages = []; return; }
        if (!Path.IsPathFullyQualified(options.ArtifactDirectory)) throw Invalid("ArtifactDirectoryInvalid");
        string directory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(options.ArtifactDirectory)) + Path.DirectorySeparatorChar;
        var verifier = new ConnectorPackageVerifier(options);
        var packages = new List<VerifiedConnectorPackage>();
        foreach (string file in options.ArtifactFiles)
        {
            if (string.IsNullOrWhiteSpace(file) || Path.GetFileName(file) != file || !file.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                throw Invalid("ArtifactPathInvalid");
            string path = Path.GetFullPath(Path.Combine(directory, file));
            if (!path.StartsWith(directory, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) throw Invalid("ArtifactPathInvalid");
            using var stream = File.OpenRead(path);
            if (stream.Length is <= 0 or > ConnectorPackageVerifier.MaximumArtifactBytes) throw Invalid("ArtifactSize");
            int length = (int)stream.Length;
            byte[] bytes = new byte[length + 1];
            int read = stream.ReadAtLeast(bytes, length, throwOnEndOfStream: false);
            if (read != length || stream.ReadByte() != -1) throw Invalid("ArtifactChanged");
            string artifact = new UTF8Encoding(false, true).GetString(bytes, 0, read);
            packages.Add(verifier.Verify(artifact));
        }
        if (packages.GroupBy(package => (package.Manifest.PackageKey, package.Manifest.PackageVersion)).Any(group => group.Count() != 1))
            throw Invalid("DuplicatePackageVersion");
        foreach (var group in packages.SelectMany(package => package.Manifest.Actions.Select(action => new { package, action }))
            .GroupBy(item => (item.action.Key, item.action.Version)))
            if (group.Select(item => item.package.Pin(item.action).ActionContractHash).Distinct(StringComparer.Ordinal).Count() != 1)
                throw Invalid("ActionContractDrift");
        Packages = packages.OrderBy(package => package.Manifest.PackageKey, StringComparer.Ordinal)
            .ThenByDescending(package => Version.Parse(package.Manifest.PackageVersion)).ToArray();
    }

    public VerifiedConnectorPackage Require(string packageKey, string version, string? digest = null)
    {
        VerifiedConnectorPackage package = Packages.SingleOrDefault(package => package.Manifest.PackageKey == packageKey && package.Manifest.PackageVersion == version)
            ?? throw Invalid("PackageUnavailable");
        if (digest is not null && digest != package.Digest) throw Invalid("PackageDigestChanged");
        return package;
    }

    public VerifiedConnectorPackage Require(ConnectorArtifactPin pin)
    {
        VerifiedConnectorPackage package = Require(pin.PackageKey, pin.PackageVersion, pin.ArtifactDigest);
        if (!package.Manifest.Actions.Any(action => package.Pin(action).ActionContractHash == pin.ActionContractHash)) throw Invalid("ActionContractDrift");
        return package;
    }

    public static void RequireNativeContracts(VerifiedConnectorPackage package, IEnumerable<IConnectorDescriptor> registrations)
    {
        if (package.Manifest.AdapterKind != "Native") throw Invalid("NativeAdapterRequired");
        ConnectorDescriptor deployed = registrations.Select(registration => registration.Descriptor).SingleOrDefault(descriptor => descriptor.Key == package.Manifest.ConnectorKey)
            ?? throw Invalid("NativeAdapterUnavailable");
        foreach (ConnectorPackageAction action in package.Manifest.Actions)
        {
            ConnectorActionDescriptor actual = deployed.Actions.SingleOrDefault(candidate => candidate.Key == action.Key && candidate.Version == action.Version)
                ?? throw Invalid("NativeActionUnavailable");
            if (action.MinimumRiskTier != (actual.Approval?.Tier.ToString() ?? "ReadOnly")
                || CanonicalJson.Hash(action.InputSchema) != CanonicalJson.Hash(actual.InputSchema) || CanonicalJson.Hash(action.OutputSchema) != CanonicalJson.Hash(actual.OutputSchema)
                || !action.RequiredScopes.Order(StringComparer.Ordinal).SequenceEqual(actual.RequiredScopes.Order(StringComparer.Ordinal))
                || !ScopeAlternatives(action.ScopeAlternatives).SequenceEqual(ScopeAlternatives(actual.ScopeAlternatives)))
                throw Invalid("NativeContractMismatch");
        }
    }

    private static IEnumerable<string> ScopeAlternatives(IReadOnlyList<IReadOnlyList<string>>? alternatives)
        => (alternatives ?? []).Select(option => string.Join('\n', option.Order(StringComparer.Ordinal))).Order(StringComparer.Ordinal);
    private static InvalidOperationException Invalid(string code) => new("ConnectorPackage." + code);
}
