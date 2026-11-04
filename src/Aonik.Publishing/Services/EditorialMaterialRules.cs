using System.Text.Json;

using Aonik.Publishing.Contracts;

namespace Aonik.Publishing.Services;

internal static class EditorialMaterialRules
{
    public static PublicationEditorialMaterial? Normalize(PublicationEditorialMaterial? material)
    {
        if (material is null) return null;
        var audience = Text(material.Audience, 32);
        var kind = Text(material.ReleaseKind, 24);
        if (audience is not ("general" or "children" or "adults")
            || kind is not ("world" or "guide" or "part" or "production" or "story" or "comic")) throw Invalid();
        if (material.RightsEvidence is null || material.RightsEvidence.Count > 20
            || material.RightsEvidence.Any(e => e is null || e.AssetIds is null || e.AssetIds.Count is < 1 or > 1024)
            || material.RightsEvidence.Sum(e => e.AssetIds.Count) > 2048) throw Invalid();
        var evidence = material.RightsEvidence.Select(e => new PublicationRightsEvidence(Text(e.RightsBasis, 1000),
            Text(e.EvidenceReference, 512), e.AssetIds.Select(a => Text(a, 120)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()))
            .OrderBy(e => JsonSerializer.Serialize(e), StringComparer.Ordinal).ToArray();
        var normalized = new PublicationEditorialMaterial(audience, Text(material.AgeRating, 120), kind,
            Text(material.ArtworkAssetId, 120, false), Text(material.Attribution, 2000), evidence);
        if (JsonSerializer.SerializeToUtf8Bytes(normalized).Length > 100 * 1024) throw Invalid();
        return normalized;
    }

    public static string? ApprovalBlock(SubmissionDeclarationView revision, PackageVerificationResult result)
    {
        if (!revision.PermissionGranted || revision.Material is not { } material) return "rights-evidence-required";
        if (material.ReleaseKind != result.Kind) return "release-kind-mismatch";
        if (!result.Assets.Any(a => a.Id == material.ArtworkAssetId && a.MediaType is "image/png" or "image/jpeg"))
            return "artwork-evidence-required";
        var covered = material.RightsEvidence.SelectMany(e => e.AssetIds).ToHashSet(StringComparer.Ordinal);
        if (!covered.SetEquals(result.Assets.Select(a => a.Id))) return "rights-asset-coverage-required";
        return null;
    }

    internal static string Text(string? value, int maximum, bool required = true)
    {
        var text = value?.Trim() ?? "";
        if ((required && text.Length == 0) || text.Length > maximum || text.Any(char.IsControl)) throw Invalid();
        return text;
    }
    private static PublishingException Invalid() => new(422, "invalid-editorial-material", "Provide bounded audience, attribution and asset rights evidence.");
    internal static string Note(string? value, int maximum, bool required = true)
    {
        var text = (value?.Trim() ?? "").Replace("\r\n", "\n");
        if ((required && text.Length == 0) || text.Length > maximum || text.Any(c => char.IsControl(c) && c != '\n')) throw Invalid();
        return text;
    }
}

internal sealed record SubmissionDeclarationView(bool PermissionGranted, PublicationEditorialMaterial? Material);
