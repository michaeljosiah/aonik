using System.Security.Cryptography;
using System.Text;

namespace Aonik.Finance.Services.Partners.Connectors.Flutterwave;

/// <summary>
/// Reference + idempotency-key helpers (Spec 037 §5.3, §7.3, G5/G12/G13). Flutterwave needs three
/// distinct values: a body <c>reference</c> (6–42 alphanumeric), an <c>X-Idempotency-Key</c> (12–255
/// alphanumeric), and its own provider id. We derive the first two deterministically from our
/// <c>ClientReference</c> so a caller retry de-dupes, and keep the original verbatim for correlation.
/// </summary>
internal static class FlutterwaveReferences
{
    /// <summary>
    /// Sanitizes an AONIK client reference into a Flutterwave-safe body <c>reference</c>:
    /// strips non-alphanumerics; if the result is empty or too long, falls back to a stable hash so
    /// the value stays unique and within 6–42 chars (G13).
    /// </summary>
    public static string SanitizeReference(string clientReference)
    {
        var stripped = new string((clientReference ?? string.Empty)
            .Where(char.IsLetterOrDigit)
            .ToArray());

        if (stripped.Length is >= 6 and <= 42)
        {
            return stripped;
        }

        // Empty / too short / too long → deterministic hash, prefixed and capped at 42.
        var hash = "AONIK" + Hex(clientReference ?? string.Empty);
        return hash[..Math.Min(42, hash.Length)];
    }

    /// <summary>Deterministic 32-hex idempotency key from a stable input (alphanumeric, 32 chars).</summary>
    public static string IdempotencyKeyFrom(string input) => Hex(input ?? string.Empty);

    /// <summary>Fresh random idempotency key for replay-harmless read-like POSTs (hyphen-free GUID).</summary>
    public static string FreshIdempotencyKey() => Guid.NewGuid().ToString("N");

    private static string Hex(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes)[..32].ToLowerInvariant();
    }
}
