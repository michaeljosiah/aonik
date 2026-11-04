using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

using Aonik.Publishing.Contracts;

namespace Aonik.Publishing.Services;

internal static class SubmissionDeclaration
{
    public static CreateSubmissionRequest Normalize(CreateSubmissionRequest request)
    {
        if (request.ClientRequestId == Guid.Empty)
            throw Invalid("A non-empty client request ID is required.");

        var normalized = request with
        {
            ChannelKey = Text(request.ChannelKey, "Channel key", 64),
            Title = Text(request.Title, "Title", 200),
            CreatorDisplayName = Text(request.CreatorDisplayName, "Creator display name", 200),
            Summary = Text(request.Summary, "Summary", 2000, required: false),
            Language = Text(request.Language, "Language", 35),
            WorldId = Text(request.WorldId, "World ID", 64),
            Edition = Text(request.Edition, "Edition", 120),
            PackageFileName = Text(request.PackageFileName, "Package filename", 255),
            DeclaredPackageSha256 = Text(request.DeclaredPackageSha256, "Package SHA-256", 64).ToLowerInvariant(),
            RightsDeclarationVersion = Text(request.RightsDeclarationVersion, "Rights declaration version", 120),
            EditorialMaterial = EditorialMaterialRules.Normalize(request.EditorialMaterial),
        };

        if (!Regex.IsMatch(normalized.ChannelKey, "^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)
            || !Regex.IsMatch(normalized.WorldId, "^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant))
            throw Invalid("Channel key and world ID must be lowercase hyphenated identifiers.");
        if (!Regex.IsMatch(normalized.DeclaredPackageSha256, "^[a-f0-9]{64}$", RegexOptions.CultureInvariant))
            throw Invalid("Package SHA-256 must contain 64 hexadecimal characters.");
        if (normalized.DeclaredPackageBytes <= 0)
            throw Invalid("Declared package length must be positive.");
        if (normalized.PackageFileName.IndexOfAny(['/', '\\', ':']) >= 0
            || !normalized.PackageFileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            throw Invalid("Package filename must be a ZIP filename without a path.");
        if (!normalized.DistributionPermissionGranted)
            throw Invalid("Distribution permission must be explicitly granted for this submission.");
        return normalized;
    }

    public static string Digest(CreateSubmissionRequest request)
    {
        // Versioned, fixed field order. Every admitted declaration field belongs to request identity.
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new
        {
            version = 1,
            request.ClientRequestId,
            request.ChannelKey,
            request.Title,
            request.CreatorDisplayName,
            request.Summary,
            request.Language,
            request.WorldId,
            request.Edition,
            request.PackageFileName,
            request.DeclaredPackageBytes,
            request.DeclaredPackageSha256,
            request.RightsDeclarationVersion,
            request.DistributionPermissionGranted,
        });
        // Keep v1 replay identity for receipts created before editorial material existed.
        if (request.EditorialMaterial is not null)
            bytes = JsonSerializer.SerializeToUtf8Bytes(new { version = 2, declarationDigest = Convert.ToHexStringLower(SHA256.HashData(bytes)), request.EditorialMaterial });
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }

    private static string Text(string? value, string field, int maximum, bool required = true)
    {
        var text = value?.Trim() ?? string.Empty;
        if ((required && text.Length == 0) || text.Length > maximum || text.Any(char.IsControl))
            throw Invalid($"{field} must be {(required ? "non-empty and " : "")}at most {maximum} characters without control characters.");
        return text;
    }

    private static PublishingException Invalid(string message) => new(422, "invalid-declaration", message);
}
