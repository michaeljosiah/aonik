using System.Security.Cryptography;
using System.Text;
using Aonik.SharedKernel.Abstractions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aonik.Infrastructure.ExternalServices.Plaid;

/// <summary>
/// Options for verifying inbound Plaid webhooks (issue H13). Bound to the same
/// <c>Finance:PersonalFinance:Plaid</c> section as the Plaid account-link gateway, so
/// verification uses the same credentials and environment. Verification is enforced
/// only when <see cref="UseRealPlaidApi"/> is true — in simulation/dev the webhooks are
/// not real and carry no Plaid signature.
/// </summary>
public sealed class PlaidWebhookVerificationOptions
{
    public bool UseRealPlaidApi { get; set; }
    public string BaseUrl { get; set; } = "https://sandbox.plaid.com";
    public string ClientId { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;

    /// <summary>
    /// Maximum age of a webhook's <c>iat</c> claim before it is rejected as a possible
    /// replay. Plaid recommends 5 minutes.
    /// </summary>
    public int MaxAgeSeconds { get; set; } = 300;
}

/// <summary>
/// Verifies the JWS signature Plaid attaches to every webhook (the
/// <c>Plaid-Verification</c> header), proving the payload genuinely came from Plaid and
/// was not tampered with. Without this, the anonymous webhook endpoints accept any forged
/// body from anyone on the internet.
/// </summary>
public interface IPlaidWebhookVerifier
{
    /// <summary>
    /// True when verification is active (real Plaid API configured). When false the
    /// middleware passes webhooks through unverified — a deliberately dev/simulation-only
    /// posture, since no real Plaid signature exists in that mode.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Returns true only if <paramref name="verificationHeader"/> is a valid ES256 JWS whose
    /// <c>request_body_sha256</c> claim matches <paramref name="rawBody"/> and whose <c>iat</c>
    /// is within the configured freshness window. Fails closed on any missing/invalid input.
    /// </summary>
    Task<bool> VerifyAsync(string? verificationHeader, ReadOnlyMemory<byte> rawBody, CancellationToken cancellationToken);
}

/// <summary>Fetches (and caches) Plaid's webhook-verification public keys by key id.</summary>
internal interface IPlaidWebhookKeyProvider
{
    Task<JsonWebKey?> GetKeyAsync(string keyId, CancellationToken cancellationToken);
}

internal sealed class PlaidWebhookVerifier : IPlaidWebhookVerifier
{
    private const string ExpectedAlgorithm = "ES256";

    private static readonly JsonWebTokenHandler TokenHandler = new();

    private readonly IPlaidWebhookKeyProvider _keyProvider;
    private readonly IClock _clock;
    private readonly PlaidWebhookVerificationOptions _options;
    private readonly ILogger<PlaidWebhookVerifier> _logger;

    public PlaidWebhookVerifier(
        IPlaidWebhookKeyProvider keyProvider,
        IClock clock,
        IOptions<PlaidWebhookVerificationOptions> options,
        ILogger<PlaidWebhookVerifier> logger)
    {
        _keyProvider = keyProvider;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsEnabled => _options.UseRealPlaidApi;

    public async Task<bool> VerifyAsync(
        string? verificationHeader,
        ReadOnlyMemory<byte> rawBody,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(verificationHeader))
        {
            _logger.LogWarning("Plaid webhook rejected: missing Plaid-Verification header.");
            return false;
        }

        JsonWebToken token;
        try
        {
            token = new JsonWebToken(verificationHeader);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Plaid webhook rejected: Plaid-Verification header is not a well-formed JWS.");
            return false;
        }

        // Pin the algorithm before touching the key: never let the token's own header talk us
        // into a different (or "none") algorithm — the classic JWT algorithm-confusion attack.
        if (!string.Equals(token.Alg, ExpectedAlgorithm, StringComparison.Ordinal))
        {
            _logger.LogWarning("Plaid webhook rejected: unexpected JWS alg {Alg}.", token.Alg);
            return false;
        }

        if (string.IsNullOrEmpty(token.Kid))
        {
            _logger.LogWarning("Plaid webhook rejected: JWS header has no kid.");
            return false;
        }

        var key = await _keyProvider.GetKeyAsync(token.Kid, cancellationToken);
        if (key is null)
        {
            _logger.LogWarning("Plaid webhook rejected: no verification key for kid {Kid}.", token.Kid);
            return false;
        }

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = false, // Plaid JWS carries no exp; iat freshness is checked below.
            IssuerSigningKey = key,
            ValidAlgorithms = [ExpectedAlgorithm],
        };

        var validation = await TokenHandler.ValidateTokenAsync(verificationHeader, validationParameters);
        if (!validation.IsValid)
        {
            _logger.LogWarning(validation.Exception, "Plaid webhook rejected: JWS signature did not validate.");
            return false;
        }

        var validated = (JsonWebToken)validation.SecurityToken;

        // Replay protection: reject a signature that is too old (or implausibly future-dated).
        if (!validated.TryGetPayloadValue<long>("iat", out var iat))
        {
            _logger.LogWarning("Plaid webhook rejected: JWS has no iat claim.");
            return false;
        }

        var age = _clock.UtcNow - DateTimeOffset.FromUnixTimeSeconds(iat).UtcDateTime;
        var maxAge = TimeSpan.FromSeconds(_options.MaxAgeSeconds);
        if (age > maxAge || age < -maxAge)
        {
            _logger.LogWarning("Plaid webhook rejected: JWS iat is outside the {MaxAge}s freshness window (age {AgeSeconds}s).",
                _options.MaxAgeSeconds, (long)age.TotalSeconds);
            return false;
        }

        // Bind the signature to the body: the JWS signs a hash of the body, not the body itself,
        // so recompute it and compare. This is what stops a valid-but-stolen signature being
        // replayed over a different payload.
        if (!validated.TryGetPayloadValue<string>("request_body_sha256", out var claimedHash)
            || string.IsNullOrEmpty(claimedHash))
        {
            _logger.LogWarning("Plaid webhook rejected: JWS has no request_body_sha256 claim.");
            return false;
        }

        var actualHash = Convert.ToHexStringLower(SHA256.HashData(rawBody.Span));
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(actualHash),
                Encoding.ASCII.GetBytes(claimedHash)))
        {
            _logger.LogWarning("Plaid webhook rejected: request body hash does not match the signed request_body_sha256.");
            return false;
        }

        return true;
    }
}
