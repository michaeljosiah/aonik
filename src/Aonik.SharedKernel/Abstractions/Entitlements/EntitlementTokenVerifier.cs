using System.Text.Json;

namespace Aonik.SharedKernel.Abstractions.Entitlements;

/// <summary>
/// The reference verifier (Spec 090 §5.1, §11) — deliberately a hundred lines a reviewer can read, not a JWT
/// library dependency.
///
/// <para>
/// Three passes, explicit and asymmetric, because Rev 2's prescribed order was impossible to execute: it said
/// "verify against the key named by <c>kid</c> … only then decode" — but <c>kid</c> lives <em>inside</em> the
/// encoded payload. You cannot select the key without decoding, and you were told not to decode before
/// verifying. An implementer would have resolved the circle however seemed natural, which is precisely the
/// divergence a wire format exists to prevent.
/// </para>
///
/// <para>
/// <strong>Pass 1</strong> decodes and reads <c>kid</c> and nothing else — no other claim may be read, cached,
/// logged or acted on. <strong>Pass 2</strong> verifies Ed25519 over every ASCII byte of the signing input with
/// that key; nothing from pass 1 survives a failure. <strong>Pass 3</strong> re-reads every claim from the
/// now-verified payload, including <c>kid</c> — and rejects if it differs from the one pass 1 used. That re-read
/// is not paranoia: an attacker controls the whole payload before verification, so pass 1 may only ever select a
/// <em>candidate</em> key, and every decision must be made from bytes a signature has covered.
/// </para>
/// </summary>
public static class EntitlementTokenVerifier
{
    /// <param name="verify">Ed25519 verification: (message, signature, publicKey) → valid.</param>
    /// <param name="resolveKey">Raw 32-byte public key for a kid, or null when unknown — which is a hard failure.</param>
    public static EntitlementVerificationResult Verify(
        string token,
        Func<string, byte[]?> resolveKey,
        Func<byte[], byte[], byte[], bool> verify,
        DateTimeOffset now)
    {
        // Envelope. More or fewer than two parts, non-alphabet characters, or padding: rejected before
        // anything is parsed, with no partial acceptance.
        if (!EntitlementTokenFormat.TrySplit(token, out var signingInput, out var signature))
        {
            return EntitlementVerificationResult.Fail(EntitlementVerdict.Malformed);
        }

        if (!EntitlementTokenFormat.TryBase64UrlDecode(signingInput, out var payloadBytes))
        {
            return EntitlementVerificationResult.Fail(EntitlementVerdict.Malformed);
        }

        // ── PASS 1 — untrusted, for key selection ONLY ───────────────────
        string candidateKid;

        try
        {
            using var untrusted = JsonDocument.Parse(payloadBytes);

            if (untrusted.RootElement.ValueKind != JsonValueKind.Object
                || !untrusted.RootElement.TryGetProperty("kid", out var kidElement)
                || kidElement.ValueKind != JsonValueKind.String)
            {
                return EntitlementVerificationResult.Fail(EntitlementVerdict.Malformed);
            }

            candidateKid = kidElement.GetString()!;
        }
        catch (JsonException)
        {
            return EntitlementVerificationResult.Fail(EntitlementVerdict.Malformed);
        }

        // Unknown kid is a HARD failure, never ignored — absence from the published set is how a
        // withdrawn key stops verifying (§6).
        var publicKey = resolveKey(candidateKid);

        if (publicKey is null)
        {
            return EntitlementVerificationResult.Fail(EntitlementVerdict.UnknownKey);
        }

        // ── PASS 2 — verify over ALL bytes of the signing input ──────────
        if (!verify(EntitlementTokenFormat.SigningBytes(signingInput), signature, publicKey))
        {
            return EntitlementVerificationResult.Fail(EntitlementVerdict.BadSignature);
        }

        // ── PASS 3 — trusted; re-read EVERYTHING ─────────────────────────
        using var payload = JsonDocument.Parse(payloadBytes);
        var root = payload.RootElement.Clone();

        if (!root.TryGetProperty("kid", out var verifiedKid)
            || !string.Equals(verifiedKid.GetString(), candidateKid, StringComparison.Ordinal))
        {
            return EntitlementVerificationResult.Fail(EntitlementVerdict.Malformed);
        }

        if (!root.TryGetProperty("v", out var version)
            || version.ValueKind != JsonValueKind.Number
            || version.GetInt32() != 1)
        {
            // An unrecognised v is a rejection; unknown CLAIMS are ignored, which is the other half of
            // forward compatibility.
            return EntitlementVerificationResult.Fail(EntitlementVerdict.Malformed);
        }

        if (!TryReadUnixSeconds(root, "exp", out var exp)
            || !TryReadUnixSeconds(root, "gra", out var gra))
        {
            return EntitlementVerificationResult.Fail(EntitlementVerdict.Malformed);
        }

        if (now > gra)
        {
            return EntitlementVerificationResult.Fail(EntitlementVerdict.Expired);
        }

        return new EntitlementVerificationResult(
            now > exp ? EntitlementVerdict.ValidInGrace : EntitlementVerdict.Valid,
            root);
    }

    private static bool TryReadUnixSeconds(JsonElement root, string claim, out DateTimeOffset value)
    {
        value = default;

        // Integer seconds since the epoch, UTC. A string or a float is a malformed token, not a
        // tolerated variant — tolerance is how two implementations drift apart.
        if (!root.TryGetProperty(claim, out var element)
            || element.ValueKind != JsonValueKind.Number
            || !element.TryGetInt64(out var seconds))
        {
            return false;
        }

        value = DateTimeOffset.FromUnixTimeSeconds(seconds);
        return true;
    }
}

public enum EntitlementVerdict
{
    Valid = 0,

    /// <summary>
    /// Past <c>exp</c>, inside <c>gra</c> — the offline case §8 exists for. The client keeps working and
    /// refreshes at the first opportunity.
    /// </summary>
    ValidInGrace = 1,

    Malformed = 2,
    UnknownKey = 3,
    BadSignature = 4,
    Expired = 5,
}

/// <param name="Payload">The verified claims. Meaningful only when the verdict is Valid or ValidInGrace.</param>
public sealed record EntitlementVerificationResult(EntitlementVerdict Verdict, JsonElement Payload)
{
    public bool IsUsable => Verdict is EntitlementVerdict.Valid or EntitlementVerdict.ValidInGrace;

    public static EntitlementVerificationResult Fail(EntitlementVerdict verdict)
        => new(verdict, default);
}
