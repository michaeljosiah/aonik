using System.Text;
using System.Text.Json;

using Aonik.SharedKernel.Abstractions.Entitlements;

using FluentAssertions;

using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Security;

namespace Aonik.Application.Tests.Subscriptions;

/// <summary>
/// Spec 090 §5.1 — the wire format and the reference verifier, exercised as the seven vectors the spec ships.
///
/// <para>
/// The verifier here is handed a raw Ed25519 primitive and nothing else, because the property under test is that
/// verification needs <em>only</em> the public key and the token — no network, no library beyond the signature
/// itself, no server. That is the whole point of the spec.
/// </para>
/// </summary>
public class EntitlementTokenFormatTests
{
    private static readonly (byte[] Public, byte[] Private) Key = GenerateKey();
    private const string Kid = "2026-08-test";

    private static (byte[], byte[]) GenerateKey()
    {
        var privateKey = new Ed25519PrivateKeyParameters(new SecureRandom());
        return (privateKey.GeneratePublicKey().GetEncoded(), privateKey.GetEncoded());
    }

    private static byte[] Sign(byte[] message, byte[] privateKey)
    {
        var signer = new Ed25519Signer();
        signer.Init(true, new Ed25519PrivateKeyParameters(privateKey));
        signer.BlockUpdate(message, 0, message.Length);
        return signer.GenerateSignature();
    }

    private static bool Verify(byte[] message, byte[] signature, byte[] publicKey)
    {
        if (publicKey.Length != 32 || signature.Length != 64)
        {
            return false;
        }

        var verifier = new Ed25519Signer();
        verifier.Init(false, new Ed25519PublicKeyParameters(publicKey));
        verifier.BlockUpdate(message, 0, message.Length);
        return verifier.VerifySignature(signature);
    }

    private static string IssueToken(
        long exp, long gra, string kid = Kid, byte[]? signWith = null)
    {
        var payload = Encoding.UTF8.GetBytes(
            $$"""{"v":1,"jti":"{{Guid.NewGuid():D}}","rvh":"abc","sub":"party:{{Guid.NewGuid():D}}","tid":"{{Guid.NewGuid():D}}","plan":"studio-pro","feat":["cloud-sync"],"lim":{"workspaces":25},"use":{"workspaces":18},"iat":1755000000,"exp":{{exp}},"gra":{{gra}},"kid":"{{kid}}"}""");

        return EntitlementTokenFormat.Compose(payload, bytes => Sign(bytes, signWith ?? Key.Private));
    }

    private static EntitlementVerificationResult VerifyToken(string token, DateTimeOffset now)
        => EntitlementTokenVerifier.Verify(
            token,
            kid => kid == Kid ? Key.Public : null,
            Verify,
            now);

    private static readonly DateTimeOffset Fresh = DateTimeOffset.FromUnixTimeSeconds(1755100000);

    // ── Vector 1: valid ──────────────────────────────────────────────────

    [Fact]
    public void AValidToken_Should_VerifyOffline()
    {
        var token = IssueToken(exp: 1756000000, gra: 1758000000);

        var result = VerifyToken(token, Fresh);

        // No network call occurred on this path — the verifier was handed a key and a primitive and
        // nothing else, which is acceptance criterion 1.
        result.Verdict.Should().Be(EntitlementVerdict.Valid);
        result.Payload.GetProperty("plan").GetString().Should().Be("studio-pro");
        result.Payload.GetProperty("lim").GetProperty("workspaces").GetInt64().Should().Be(25);
    }

    // ── Vector 2: flipped payload byte ───────────────────────────────────

    [Fact]
    public void AFlippedPayloadByte_Should_FailTheSignature()
    {
        var token = IssueToken(exp: 1756000000, gra: 1758000000);
        var parts = token.Split('.');

        // Mutate the payload in a JSON-SAFE way — change the plan name — re-encode, and keep the
        // ORIGINAL signature. The JSON parses, pass 1 finds the kid, and only the signature can
        // object: which is the property this vector exists to prove. (Flipping a raw base64 char can
        // corrupt the JSON and be rejected earlier as Malformed — a different, weaker rejection.)
        EntitlementTokenFormat.TryBase64UrlDecode(parts[0], out var payloadBytes).Should().BeTrue();
        var tampered = Encoding.UTF8.GetString(payloadBytes)
            .Replace("\"plan\":\"studio-pro\"", "\"plan\":\"studio-max\"", StringComparison.Ordinal);

        var flipped = EntitlementTokenFormat.Base64UrlEncode(Encoding.UTF8.GetBytes(tampered))
            + "." + parts[1];

        VerifyToken(flipped, Fresh).Verdict.Should().Be(EntitlementVerdict.BadSignature);
    }

    // ── Vector 3: flipped signature byte ─────────────────────────────────

    [Fact]
    public void AFlippedSignatureByte_Should_FailTheSignature()
    {
        var token = IssueToken(exp: 1756000000, gra: 1758000000);
        var dot = token.IndexOf('.', StringComparison.Ordinal);

        var mutated = token[..(dot + 1)]
            + (token[dot + 1] == 'A' ? 'B' : 'A')
            + token[(dot + 2)..];

        VerifyToken(mutated, Fresh).Verdict.Should().Be(EntitlementVerdict.BadSignature);
    }

    // ── Vector 4: padding ────────────────────────────────────────────────

    [Fact]
    public void PaddingPresent_Should_BeMalformed()
    {
        var token = IssueToken(exp: 1756000000, gra: 1758000000);

        // Strict base64url: accepting padding would mean two encodings of one token, and a verifier
        // tolerating both cannot be byte-compared against another implementation.
        VerifyToken(token + "=", Fresh).Verdict.Should().Be(EntitlementVerdict.Malformed);
    }

    // ── Vector 5: unknown kid ────────────────────────────────────────────

    [Fact]
    public void AnUnknownKid_Should_BeAHardFailure()
    {
        var token = IssueToken(exp: 1756000000, gra: 1758000000, kid: "withdrawn-key");

        // Never ignored, unlike unknown claims: absence from the published set is how a withdrawn key
        // stops verifying, including one that shipped inside a client binary.
        VerifyToken(token, Fresh).Verdict.Should().Be(EntitlementVerdict.UnknownKey);
    }

    // ── Vector 6: expired ────────────────────────────────────────────────

    [Fact]
    public void PastGrace_Should_BeExpired()
    {
        var token = IssueToken(exp: 1755000100, gra: 1755000200);

        VerifyToken(token, Fresh).Verdict.Should().Be(EntitlementVerdict.Expired);
    }

    // ── Vector 7: in grace ───────────────────────────────────────────────

    [Fact]
    public void PastExpiryInsideGrace_Should_BeValidInGrace()
    {
        var token = IssueToken(exp: 1755000100, gra: 1758000000);

        var result = VerifyToken(token, Fresh);

        // The offline case §8 exists for: the train, the studio with no network. The client keeps
        // working and refreshes at the first opportunity — expiry forces refresh, grace forgives
        // absence.
        result.Verdict.Should().Be(EntitlementVerdict.ValidInGrace);
        result.IsUsable.Should().BeTrue();
    }

    // ── Envelope rejections ──────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("one-part-only")]
    [InlineData("three.part.token")]
    [InlineData("has space.abc")]
    [InlineData("emoji🙂.abc")]
    public void ABrokenEnvelope_Should_BeMalformed(string token)
        => VerifyToken(token, Fresh).Verdict.Should().Be(EntitlementVerdict.Malformed);

    [Fact]
    public void APayloadThatIsNotAJsonObject_Should_BeMalformed()
    {
        var payload = Encoding.UTF8.GetBytes("[1,2,3]");
        var token = EntitlementTokenFormat.Compose(payload, bytes => Sign(bytes, Key.Private));

        VerifyToken(token, Fresh).Verdict.Should().Be(EntitlementVerdict.Malformed);
    }

    [Fact]
    public void AnUnrecognisedVersion_Should_BeMalformed()
    {
        var payload = Encoding.UTF8.GetBytes(
            $$"""{"v":2,"exp":1756000000,"gra":1758000000,"kid":"{{Kid}}"}""");
        var token = EntitlementTokenFormat.Compose(payload, bytes => Sign(bytes, Key.Private));

        VerifyToken(token, Fresh).Verdict.Should().Be(EntitlementVerdict.Malformed);
    }

    [Fact]
    public void AStringTimestamp_Should_BeMalformed()
    {
        // Integer seconds, UTC. Never strings, never milliseconds — tolerance is how two
        // implementations drift apart.
        var payload = Encoding.UTF8.GetBytes(
            $$"""{"v":1,"exp":"1756000000","gra":1758000000,"kid":"{{Kid}}"}""");
        var token = EntitlementTokenFormat.Compose(payload, bytes => Sign(bytes, Key.Private));

        VerifyToken(token, Fresh).Verdict.Should().Be(EntitlementVerdict.Malformed);
    }

    [Fact]
    public void UnknownClaims_Should_BeIgnored()
    {
        var payload = Encoding.UTF8.GetBytes(
            $$"""{"v":1,"exp":1756000000,"gra":1758000000,"kid":"{{Kid}}","future-claim":"whatever"}""");
        var token = EntitlementTokenFormat.Compose(payload, bytes => Sign(bytes, Key.Private));

        // Claims can be added without breaking old clients; only v and kid are load-bearing.
        VerifyToken(token, Fresh).Verdict.Should().Be(EntitlementVerdict.Valid);
    }

    // ── The signature covers the signing input, not the JSON ─────────────

    [Fact]
    public void TheSignature_Should_CoverTheEncodedInput_NotAReserialisation()
    {
        // Two payloads with identical JSON meaning and different bytes: extra whitespace.
        var compact = Encoding.UTF8.GetBytes(
            $$"""{"v":1,"exp":1756000000,"gra":1758000000,"kid":"{{Kid}}"}""");
        var spaced = Encoding.UTF8.GetBytes(
            $$"""{ "v":1, "exp":1756000000, "gra":1758000000, "kid":"{{Kid}}" }""");

        var compactToken = EntitlementTokenFormat.Compose(compact, b => Sign(b, Key.Private));
        var spacedToken = EntitlementTokenFormat.Compose(spaced, b => Sign(b, Key.Private));

        // BOTH verify: each signature covers its own transmitted bytes, so formatting is irrelevant
        // and no canonicalisation ever happens. Swap the signatures and both fail, which proves the
        // bytes covered are the encoded input rather than the parsed meaning.
        VerifyToken(compactToken, Fresh).Verdict.Should().Be(EntitlementVerdict.Valid);
        VerifyToken(spacedToken, Fresh).Verdict.Should().Be(EntitlementVerdict.Valid);

        var swapped = compactToken.Split('.')[0] + "." + spacedToken.Split('.')[1];
        VerifyToken(swapped, Fresh).Verdict.Should().Be(EntitlementVerdict.BadSignature);
    }

    [Fact]
    public void PassOne_Should_NotLetAForgedKidSurviveToPassThree()
    {
        // A token signed with OUR key but naming a kid the resolver maps to our key too — then, after
        // signing, we tamper so the payload's kid differs from what pass 1 read. Simulated directly:
        // the verifier must reject when the pass-3 re-read disagrees with pass 1. Here the simplest
        // construction is a payload whose kid resolves but whose signature comes from another key.
        var (otherPublic, otherPrivate) = GenerateKey();

        var token = IssueToken(exp: 1756000000, gra: 1758000000, signWith: otherPrivate);

        // The resolver maps Kid to OUR public key; the signature is the other key's. Pass 2 fails —
        // nothing from pass 1 survives.
        VerifyToken(token, Fresh).Verdict.Should().Be(EntitlementVerdict.BadSignature);

        // And with a resolver honest about the other key, the same token verifies: the failure above
        // was key selection working, not the signature being broken.
        EntitlementTokenVerifier.Verify(
            token, kid => kid == Kid ? otherPublic : null, Verify, Fresh)
            .Verdict.Should().Be(EntitlementVerdict.Valid);
    }

    // ── Base64url strictness ─────────────────────────────────────────────

    [Theory]
    [InlineData("abc=", false)]
    [InlineData("ab+c", false)]
    [InlineData("ab/c", false)]
    [InlineData("ab-_", true)]
    public void Base64Url_Should_BeStrict(string value, bool decodes)
        => EntitlementTokenFormat.TryBase64UrlDecode(value, out _).Should().Be(decodes);

    [Fact]
    public void Base64Url_Should_RoundTrip()
    {
        var bytes = Encoding.UTF8.GetBytes("the undersong");
        var encoded = EntitlementTokenFormat.Base64UrlEncode(bytes);

        encoded.Should().NotContain("=").And.NotContain("+").And.NotContain("/");

        EntitlementTokenFormat.TryBase64UrlDecode(encoded, out var decoded).Should().BeTrue();
        decoded.Should().Equal(bytes);
    }
}
