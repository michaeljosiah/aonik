using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using Aonik.Publishing.Contracts;
using Aonik.Publishing.Services;

namespace Aonik.Infrastructure.Publishing;

internal static class VerifierProtocol
{
    internal static PackageVerificationResult Parse(string json, VerifiedInput input, PackageVerifierOptions policy)
    {
        var result = JsonSerializer.Deserialize<PackageVerificationResult>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        if (result is null || result.ReaderCommit != DockerPackageVerifier.ReaderPin || result.Assets is null
            || !Regex.IsMatch(result.Code ?? "", "^[a-z0-9-]{1,100}$"))
            return Refused("verifier-protocol-invalid", policy);
        if (!result.Passed) return result;
        if (result.Code != "verified" || result.ArchiveSha256 != input.Sha256 || result.ArchiveBytes != input.Bytes
            || string.IsNullOrWhiteSpace(result.WorldId) || string.IsNullOrWhiteSpace(result.Edition)
            || result.Assets.Count > policy.MaxAssets || result.Kind is not ("world" or "guide" or "part" or "production" or "story" or "comic")
            || result.Assets.Any(a => a is null || a.ByteLength < 0 || a.ByteLength > policy.MaxAssetBytes || a.Path is null || a.Path.Length > 1024
                || !Regex.IsMatch(a.Sha256 ?? "", "^[a-f0-9]{64}$"))
            || !Regex.IsMatch(result.Fingerprint ?? "", "^[a-f0-9]{64}$"))
            return Refused("verifier-protocol-invalid", policy);
        var lines = new[] { result.Edition }.Concat(result.Assets.Select(a => $"{a.Path} {a.Sha256}").Order(StringComparer.Ordinal));
        if (Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', lines)))) != result.Fingerprint)
            return Refused("verifier-fingerprint-invalid", policy);
        return result;
    }

    internal static PackageVerificationResult Refused(string code, PackageVerifierOptions policy)
        => new(false, code, policy.ReaderCommit, "", 0, "", "", "", "", null, null, null, []);
}
