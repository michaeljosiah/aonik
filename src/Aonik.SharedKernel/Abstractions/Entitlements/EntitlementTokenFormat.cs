using System.Text;

namespace Aonik.SharedKernel.Abstractions.Entitlements;

/// <summary>
/// The wire format, to the byte (Spec 090 §5.1).
///
/// <para>
/// An earlier draft described the format in prose and then asked, in its acceptance criteria, that an independent
/// implementation in a second language verify the same tokens. Those two things were incompatible: the prose
/// fixed neither the envelope, nor the exact bytes covered by the signature, nor the character encoding, nor —
/// the one that silently breaks everything — <strong>whether the JSON is verified as received or re-serialised
/// first</strong>. Two correct implementations would then disagree about key order, whitespace and number
/// formatting, and reject each other's valid tokens.
/// </para>
///
/// <para>
/// <code>token = SIGNING_INPUT "." BASE64URL(signature)</code>, where <c>SIGNING_INPUT</c> is
/// <c>BASE64URL(payload_bytes)</c> and the signature covers <strong>every ASCII byte of
/// <c>SIGNING_INPUT</c></strong> — up to but not including the separator. Never the payload bytes, never a
/// re-serialisation, and never a fixed-length prefix.
/// </para>
///
/// <para>
/// <strong>Canonicalisation is not required and must not be attempted.</strong> A verifier splits on the single
/// <c>"."</c>, verifies over the left-hand substring exactly as it arrived, and only then decodes it. Key order,
/// whitespace and number formatting become irrelevant because no implementation ever re-encodes anything — the
/// same reason JWS is specified this way, and the one choice that makes a second-language implementation
/// mechanical.
/// </para>
/// </summary>
public static class EntitlementTokenFormat
{
    /// <summary>RFC 4648 §5, unpadded: <c>-</c> and <c>_</c>, no <c>=</c>.</summary>
    public static string Base64UrlEncode(ReadOnlySpan<byte> bytes)
        => Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    /// <summary>
    /// Decodes base64url, refusing padding and any character outside the alphabet.
    ///
    /// <para>
    /// Strict rather than forgiving: accepting padding would mean two encodings of one token, and a verifier that
    /// tolerates both cannot be byte-compared against another implementation.
    /// </para>
    /// </summary>
    public static bool TryBase64UrlDecode(string value, out byte[] bytes)
    {
        bytes = [];

        if (string.IsNullOrEmpty(value) || value.Contains('=', StringComparison.Ordinal))
        {
            return false;
        }

        foreach (var c in value)
        {
            var allowed = c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-' or '_';

            if (!allowed)
            {
                return false;
            }
        }

        var padded = value.Replace('-', '+').Replace('_', '/');
        padded += (value.Length % 4) switch { 2 => "==", 3 => "=", 0 => "", _ => null! };

        if (padded is null)
        {
            return false;
        }

        try
        {
            bytes = Convert.FromBase64String(padded);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>
    /// Splits a token into the signing input and the signature.
    ///
    /// <para>
    /// The signing input is returned as the <em>original substring</em>, not a re-encoding, because that is what
    /// the signature covers.
    /// </para>
    /// </summary>
    public static bool TrySplit(string token, out string signingInput, out byte[] signature)
    {
        signingInput = string.Empty;
        signature = [];

        if (string.IsNullOrEmpty(token))
        {
            return false;
        }

        var parts = token.Split('.');

        // Exactly two. More or fewer is a rejection before anything is parsed — no partial acceptance.
        if (parts.Length != 2)
        {
            return false;
        }

        if (!TryBase64UrlDecode(parts[0], out _) || !TryBase64UrlDecode(parts[1], out var sig))
        {
            return false;
        }

        signingInput = parts[0];
        signature = sig;
        return true;
    }

    /// <summary>The exact bytes a signature is computed over: the ASCII of the signing input.</summary>
    public static byte[] SigningBytes(string signingInput) => Encoding.ASCII.GetBytes(signingInput);

    /// <summary>Assembles a token from a payload and a signer.</summary>
    public static string Compose(ReadOnlySpan<byte> payloadBytes, Func<byte[], byte[]> sign)
    {
        var signingInput = Base64UrlEncode(payloadBytes);
        var signature = sign(SigningBytes(signingInput));

        return $"{signingInput}.{Base64UrlEncode(signature)}";
    }
}
