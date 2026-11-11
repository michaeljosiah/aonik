using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using Aonik.Publishing.Contracts;
using Aonik.Publishing.Entities;

namespace Aonik.Publishing.Services;

internal static class EditorialReviewEvidence
{
    public static (string Json, string Policy, string Digest, PackageVerificationResult Package) Build(
        PublicationSubmission submission, SubmissionRevision revision, PublicationValidationReport? report,
        PublicationNamespace? space, PublishingChannelOptions channel, PublishingOptions options)
    {
        if (report is null || report.Id != revision.CurrentValidationReportId || report.Status != "passed" || report.CompletedAt is null
            || report.RevisionId != revision.Id || report.SubmissionId != submission.Id || revision.PackageVerifiedAt is null)
            throw Blocked("passing-validation-required");
        if (report.PolicyJson != JsonSerializer.Serialize(options.Verifier) || channel.RightsDeclarationVersion != revision.RightsDeclarationVersion
            || !channel.NamespaceAdmissionEnabled || revision.DeclaredPackageBytes > channel.MaxPackageBytes)
            throw Blocked("review-policy-changed");
        PackageVerificationResult? package;
        try { package = JsonSerializer.Deserialize<PackageVerificationResult>(report.ResultJson); }
        catch (JsonException) { throw Blocked("validation-evidence-unavailable"); }
        if (package is null || !package.Passed || package.Code != "verified" || package.ReaderCommit != report.ReaderCommit
            || package.ReaderCommit != options.Verifier.ReaderCommit || report.VerifierImage != options.Verifier.Image
            || !Regex.IsMatch(report.VerifierImage, "^[A-Za-z0-9./:_-]+@sha256:[a-f0-9]{64}$")
            || report.PackageSha256 != revision.DeclaredPackageSha256 || report.PackageBytes != revision.DeclaredPackageBytes
            || package.ArchiveSha256 != report.PackageSha256 || package.ArchiveBytes != report.PackageBytes
            || package.WorldId != revision.WorldId || package.Edition != revision.Edition || package.Assets is null
            || package.Assets.Count > options.Verifier.MaxAssets || package.Assets.Any(a => a is null)
            || package.Assets.Select(a => a.Id).Distinct(StringComparer.Ordinal).Count() != package.Assets.Count)
            throw Blocked("validation-evidence-unavailable");
        var fingerprint = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n',
            new[] { package.Edition }.Concat(package.Assets.Select(a => $"{a.Path} {a.Sha256}").Order(StringComparer.Ordinal))))));
        if (fingerprint != package.Fingerprint) throw Blocked("validation-evidence-unavailable");
        if (package.Presentation is not { SchemaVersion: >= 1 and <= 7 } presentation
            || !Regex.IsMatch(presentation.ManifestSha256 ?? "", "^[a-f0-9]{64}$") || presentation.Manifest.ValueKind != JsonValueKind.Object
            || !presentation.Manifest.TryGetProperty("id", out var world) || world.ValueKind != JsonValueKind.String || world.GetString() != revision.WorldId
            || !presentation.Manifest.TryGetProperty("edition", out var edition) || edition.ValueKind != JsonValueKind.String || edition.GetString() != revision.Edition
            || !presentation.Manifest.TryGetProperty("productions", out var productions) || productions.ValueKind != JsonValueKind.Array)
            throw Blocked("review-presentation-unavailable");
        if (space is null || space.CreatorPartyId != submission.CreatorPartyId
            || (package.Kind == "part" && (space.ApprovedBaseFingerprint is null || package.BaseFingerprint != space.ApprovedBaseFingerprint
                || package.PartNumber != space.LastApprovedPartNumber + 1))) throw Blocked("review-lineage-unavailable");
        var policy = JsonSerializer.Serialize(new { version = 1, channel.ReviewPolicyVersion, submission.ChannelKey, submission.TenantId,
            channel.RightsDeclarationVersion, channel.MaxPackageBytes, channel.NamespaceAdmissionEnabled, verifier = options.Verifier });
        var material = JsonSerializer.Serialize(new
        {
            version = 1, submission.TenantId, submission.Id, submission.CreatorPartyId, submission.OriginatingUserId, submission.ChannelKey,
            revision = new { revision.Id, revision.Number, revision.OriginatingUserId, revision.CreatedAt, revision.RequestDigestVersion,
                revision.RequestDigest, revision.Title, revision.CreatorDisplayName, revision.Summary, revision.Language, revision.WorldId,
                revision.Edition, revision.PackageFileName, revision.DeclaredPackageBytes, revision.DeclaredPackageSha256,
                revision.DistributionPermissionGranted, revision.RightsDeclarationVersion,
                editorial = revision.EditorialMaterialJson is null ? (JsonElement?)null : JsonSerializer.Deserialize<JsonElement>(revision.EditorialMaterialJson) },
            validation = new { report.Id, report.CompletedAt, report.ReaderCommit, report.VerifierImage,
                policy = JsonSerializer.Deserialize<JsonElement>(report.PolicyJson), package },
            policy = JsonSerializer.Deserialize<JsonElement>(policy),
        });
        if (Encoding.UTF8.GetByteCount(material) > PackageCapacity.Resolve(options.Verifier.CapacityProfile).ReviewBytes)
            throw Blocked("review-snapshot-too-large");
        return (material, policy, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(material))), package);
    }

    public static PublishingException Blocked(string code) => new(422, code, "Current exact package, policy and review evidence are required before a decision.");
}
