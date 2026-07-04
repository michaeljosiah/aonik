using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aonik.Infrastructure.ExternalServices.Plaid;
using Aonik.SharedKernel.Abstractions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Aonik.Infrastructure.Tests.ExternalServices.Plaid;

/// <summary>
/// Crypto tests for the Plaid webhook JWS verifier (issue H13). Each case builds a real
/// ES256 token with a generated key and a fake key provider, so the happy path and every
/// rejection mode are exercised end-to-end without touching Plaid.
/// </summary>
public class PlaidWebhookVerifierTests
{
    private const string Kid = "test-key-1";
    private static readonly DateTime Now = new(2026, 7, 5, 12, 0, 0, DateTimeKind.Utc);
    private static readonly byte[] Body = Encoding.UTF8.GetBytes("""{"webhook_type":"TRANSACTIONS","webhook_code":"SYNC_UPDATES_AVAILABLE","item_id":"item-123"}""");

    private sealed class FakeClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class FakeKeyProvider(JsonWebKey? key, string kid) : IPlaidWebhookKeyProvider
    {
        public Task<JsonWebKey?> GetKeyAsync(string keyId, CancellationToken cancellationToken)
            => Task.FromResult(keyId == kid ? key : null);
    }

    private static string BodySha256(byte[] body) => Convert.ToHexStringLower(SHA256.HashData(body));

    private static (PlaidWebhookVerifier verifier, ECDsa signingKey) CreateVerifier(
        string? knownKid = Kid, DateTime? now = null)
    {
        var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var pub = ecdsa.ExportParameters(includePrivateParameters: false);
        var jwk = new JsonWebKey
        {
            Kty = "EC",
            Crv = "P-256",
            X = Base64UrlEncoder.Encode(pub.Q.X),
            Y = Base64UrlEncoder.Encode(pub.Q.Y),
            Kid = Kid,
            Alg = "ES256",
            Use = "sig",
        };

        var provider = new FakeKeyProvider(jwk, knownKid ?? "no-such-kid");
        var options = Options.Create(new PlaidWebhookVerificationOptions
        {
            UseRealPlaidApi = true,
            MaxAgeSeconds = 300,
        });

        var verifier = new PlaidWebhookVerifier(
            provider, new FakeClock(now ?? Now), options, NullLogger<PlaidWebhookVerifier>.Instance);

        return (verifier, ecdsa);
    }

    private static string CreateJws(ECDsa signingKey, string alg, string kid, long iat, string bodySha256)
    {
        var header = new Dictionary<string, object> { ["alg"] = alg, ["kid"] = kid, ["typ"] = "JWT" };
        var payload = new Dictionary<string, object> { ["iat"] = iat, ["request_body_sha256"] = bodySha256 };

        var headerSegment = Base64UrlEncoder.Encode(JsonSerializer.SerializeToUtf8Bytes(header));
        var payloadSegment = Base64UrlEncoder.Encode(JsonSerializer.SerializeToUtf8Bytes(payload));
        var signingInput = $"{headerSegment}.{payloadSegment}";

        // JWS ES256 signatures are the raw r||s concatenation (IEEE P1363), not DER.
        var signature = signingKey.SignData(
            Encoding.ASCII.GetBytes(signingInput), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

        return $"{signingInput}.{Base64UrlEncoder.Encode(signature)}";
    }

    private static long Iat(DateTime utc) => new DateTimeOffset(utc, TimeSpan.Zero).ToUnixTimeSeconds();

    [Fact]
    public async Task VerifyAsync_Should_ReturnTrue_When_SignatureBodyAndFreshnessAllValid()
    {
        var (verifier, key) = CreateVerifier();
        var jws = CreateJws(key, "ES256", Kid, Iat(Now), BodySha256(Body));

        (await verifier.VerifyAsync(jws, Body, CancellationToken.None)).Should().BeTrue();
    }

    [Fact]
    public async Task VerifyAsync_Should_ReturnFalse_When_HeaderMissing()
    {
        var (verifier, _) = CreateVerifier();

        (await verifier.VerifyAsync(null, Body, CancellationToken.None)).Should().BeFalse();
        (await verifier.VerifyAsync("", Body, CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task VerifyAsync_Should_ReturnFalse_When_AlgorithmIsNotEs256()
    {
        // Algorithm-confusion guard: the header claims HS256 even though it was ES256-signed.
        var (verifier, key) = CreateVerifier();
        var jws = CreateJws(key, "HS256", Kid, Iat(Now), BodySha256(Body));

        (await verifier.VerifyAsync(jws, Body, CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task VerifyAsync_Should_ReturnFalse_When_BodyHashDoesNotMatch()
    {
        // Signature is valid, but over a hash of a DIFFERENT body — the replay-over-new-payload case.
        var (verifier, key) = CreateVerifier();
        var otherBody = Encoding.UTF8.GetBytes("""{"webhook_type":"ITEM","webhook_code":"USER_PERMISSION_REVOKED"}""");
        var jws = CreateJws(key, "ES256", Kid, Iat(Now), BodySha256(otherBody));

        (await verifier.VerifyAsync(jws, Body, CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task VerifyAsync_Should_ReturnFalse_When_IatIsStale()
    {
        var (verifier, key) = CreateVerifier();
        var jws = CreateJws(key, "ES256", Kid, Iat(Now.AddMinutes(-10)), BodySha256(Body));

        (await verifier.VerifyAsync(jws, Body, CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task VerifyAsync_Should_ReturnFalse_When_KeyIdIsUnknown()
    {
        var (verifier, key) = CreateVerifier(knownKid: "different-kid");
        var jws = CreateJws(key, "ES256", Kid, Iat(Now), BodySha256(Body));

        (await verifier.VerifyAsync(jws, Body, CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task VerifyAsync_Should_ReturnFalse_When_SignatureIsTampered()
    {
        var (verifier, key) = CreateVerifier();
        var jws = CreateJws(key, "ES256", Kid, Iat(Now), BodySha256(Body));

        // Flip the last character of the signature segment.
        var lastChar = jws[^1];
        var tampered = jws[..^1] + (lastChar == 'A' ? 'B' : 'A');

        (await verifier.VerifyAsync(tampered, Body, CancellationToken.None)).Should().BeFalse();
    }
}
