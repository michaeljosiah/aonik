using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using Aonik.SharedKernel.Abstractions.Workspaces;

namespace Aonik.Workspaces.Services;

/// <summary>
/// Turns a manifest into the two things a commit needs from it: normalised paths, and one hash that identifies
/// the request (Spec 089 §6.1.1, §12).
///
/// <para>
/// Named for what it does rather than after the spec's phrase "canonical request hash", because the module's own
/// boundary test refuses any type name carrying a product word — and Arke's <em>canon</em> is one. The substring
/// match is blunt on purpose: vocabulary in platform types teaches the next contributor what the thing is for,
/// and Spec 086 paid three review rounds for getting that wrong once already.
/// </para>
/// </summary>
public static class ManifestNormaliser
{
    /// <summary>
    /// Normalises a path to forward slashes and NFC.
    ///
    /// <para>
    /// §12 treats this as a <strong>security</strong> property rather than tidiness. Unnormalised paths are how
    /// traversal gets in — and, less obviously, how case- and composition-collisions do: <c>café</c> written as
    /// <c>e</c>+combining-acute and as the precomposed <c>é</c> are different strings and the same filename to a
    /// user, so a manifest carrying both would present one file twice and let the second silently overwrite the
    /// first on any client that normalises.
    /// </para>
    /// </summary>
    public static string NormalisePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var forwardSlashed = path.Replace('\\', '/').Trim();

        // Collapse repeated separators and strip a leading one: "a//b" and "/a/b" name the same file as "a/b",
        // and leaving them distinct would let one tree hold three entries for it.
        var segments = forwardSlashed
            .Split('/', StringSplitOptions.RemoveEmptyEntries);

        foreach (var segment in segments)
        {
            // Rejected rather than resolved. Resolving "a/../b" server-side means accepting a path that was
            // trying to leave the tree and quietly deciding what it meant — the client should send what it means.
            if (segment is "." or "..")
            {
                throw new ArgumentException(
                    $"Path '{path}' contains a relative segment; manifests carry resolved paths only.",
                    nameof(path));
            }
        }

        return string.Join('/', segments).Normalize(NormalizationForm.FormC);
    }

    /// <summary>
    /// SHA-256 over the workspace id, the declared parent, and the manifest as a sorted <c>path→hash</c> list
    /// (§6.1.1).
    ///
    /// <para>
    /// This is what stops a retry replaying an outcome for a <em>different</em> tree. After a timeout a client
    /// does not know whether the commit landed, the author may have kept working, and the client correctly reuses
    /// its <c>CommitId</c> — but rebuilds the manifest from a tree that has since changed. Replaying the original
    /// outcome would tell it the new tree is committed when it is not, and the next pull would treat those edits
    /// as absent: work lost silently, with a success response on the record.
    /// </para>
    ///
    /// <para>
    /// Sorted, because two clients enumerating the same tree in different orders describe the same tree. Order
    /// sensitivity would turn an honest retry into a 409.
    /// </para>
    /// </summary>
    public static string ComputeRequestHash(
        Guid workspaceId, Guid? parentRevisionId, IReadOnlyList<ManifestEntry> manifest)
    {
        var builder = new StringBuilder();
        builder.Append(workspaceId.ToString("N")).Append('\n');
        builder.Append(parentRevisionId?.ToString("N") ?? "root").Append('\n');

        foreach (var entry in manifest
            .Select(e => (Path: NormalisePath(e.Path), e.ContentHash, e.SizeBytes))
            .OrderBy(e => e.Path, StringComparer.Ordinal))
        {
            builder
                .Append(entry.Path).Append('\0')
                .Append(entry.ContentHash.ToLowerInvariant()).Append('\0')
                .Append(entry.SizeBytes.ToString(CultureInfo.InvariantCulture))
                .Append('\n');
        }

        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()))).ToLowerInvariant();
    }

    /// <summary>
    /// Normalises every entry and refuses a manifest naming one path twice.
    ///
    /// <para>
    /// Duplicates matter after normalisation, not before: two entries can arrive looking different and normalise
    /// to the same path. Accepting them would store a tree that cannot be materialised on any filesystem, and the
    /// second write would win by accident.
    /// </para>
    /// </summary>
    public static IReadOnlyList<ManifestEntry> Normalise(IReadOnlyList<ManifestEntry> manifest)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var normalised = new List<ManifestEntry>(manifest.Count);

        foreach (var entry in manifest)
        {
            var path = NormalisePath(entry.Path);

            if (!seen.Add(path))
            {
                throw new ArgumentException(
                    $"Manifest names '{path}' more than once after normalisation.", nameof(manifest));
            }

            normalised.Add(entry with { Path = path, ContentHash = entry.ContentHash.ToLowerInvariant() });
        }

        return normalised;
    }
}
