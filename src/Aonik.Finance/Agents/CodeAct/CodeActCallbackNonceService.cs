using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aonik.SharedKernel.Abstractions.Agents;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Aonik.Finance.Agents.CodeAct;

/// <summary>
/// Mints and validates the HMAC-signed nonces that authenticate
/// <c>POST /ai/codeact/call-tool/{nonce}</c> callbacks from the ACA Dynamic
/// Sessions Python sandbox. Also tracks the per-nonce callback budget so a
/// runaway Python loop can't fan out unbounded calls against our API.
/// </summary>
/// <remarks>
/// <para>
/// The nonce binds (runId, sub-agent, tenant, user, tool whitelist, expiry,
/// jti). The signature is HMAC-SHA256 over the JSON payload using a 32-byte
/// secret loaded from <c>Ai:CodeAct:NonceSigningKey</c> (Key Vault reference
/// in production). Same plumbing pattern as <c>Verification:HashKey</c> for
/// the OTP code hashing in <c>VerificationService</c>.
/// </para>
/// <para>
/// Budget tracking is in-memory per API replica. Replicas don't share
/// nonces because the issuing replica is the only one that minted a given
/// <c>jti</c> — every callback from the Python sandbox lands on the same
/// replica that issued the nonce, courtesy of Azure Container Apps' default
/// sticky-session ingress behaviour. If we move to a multi-region pool
/// later, swap this for a distributed cache.
/// </para>
/// </remarks>
/// <summary>
/// Reason buckets surfaced in the callback endpoint's 401 body so a developer
/// can see WHY validation failed without inspecting server logs (which aren't
/// always accessible from automated test harnesses).
/// </summary>
public enum NonceValidationResult
{
    Valid,
    MissingOrEmpty,
    MalformedSegmentCount,
    WrongVersionPrefix,
    PayloadNotBase64Url,
    SignatureNotBase64Url,
    SigningKeyUnavailable,
    SignatureMismatch,
    PayloadNotJson,
    PayloadEmpty,
    Expired,
}

public sealed class CodeActCallbackNonceService
{
    private const string Version = "nonce_v1";
    private const string ConfigKey = "Ai:CodeAct:NonceSigningKey";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly Lazy<byte[]> _signingKey;
    private readonly ILogger<CodeActCallbackNonceService> _logger;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Per-nonce remaining callback budget. Key = jti. Decremented on every
    /// successful <see cref="TryConsumeBudget"/>; entry is removed when the
    /// nonce expires.
    /// </summary>
    private readonly ConcurrentDictionary<string, int> _budget = new(StringComparer.Ordinal);

    public CodeActCallbackNonceService(
        IConfiguration configuration,
        ILogger<CodeActCallbackNonceService> logger,
        TimeProvider? timeProvider = null)
    {
        // Lazy key resolution: FastEndpoints constructs every endpoint
        // (including CodeActCallbackEndpoint) eagerly at UseFastEndpoints()
        // time, which transitively constructs this service. We can't throw
        // here when the key is unset because that would prevent the API from
        // starting in any environment that uses the Hyperlight or Disabled
        // provider. Instead, defer the key check until the nonce service is
        // actually used.
        _signingKey = new Lazy<byte[]>(() => ResolveKeyOrThrow(configuration));
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    private static byte[] ResolveKeyOrThrow(IConfiguration configuration)
    {
        var keyRaw = configuration[ConfigKey];
        if (string.IsNullOrWhiteSpace(keyRaw))
        {
            throw new InvalidOperationException(
                $"Configuration value '{ConfigKey}' is required when the AcaSessions CodeAct provider is enabled. " +
                "Provide a 32-byte secret (hex or base64) via Key Vault or appsettings.");
        }
        var bytes = DecodeKey(keyRaw);
        if (bytes.Length < 32)
        {
            throw new InvalidOperationException(
                $"Configuration value '{ConfigKey}' must decode to at least 32 bytes (got {bytes.Length}).");
        }
        return bytes;
    }

    /// <summary>
    /// Mints a new nonce. The returned string is safe to embed in a URL path
    /// (base64url, no padding) and is opaque to the holder — the signing key
    /// never leaves this process.
    /// </summary>
    public string Issue(
        CodeActSandboxContext ctx,
        IReadOnlySet<string> allowedToolNames,
        int maxCallbacks,
        TimeSpan ttl)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(allowedToolNames);
        if (maxCallbacks <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxCallbacks), "Budget must be positive.");
        }
        if (ttl <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(ttl), "TTL must be positive.");
        }

        var jti = Guid.NewGuid().ToString("N");
        var payload = new CodeActCallbackPayload(
            RunId: ctx.RunId,
            SubAgentName: ctx.SubAgentName,
            TenantId: ctx.TenantId,
            UserId: ctx.CurrentUserId,
            ToolWhitelist: allowedToolNames.ToArray(),
            ExpiresAtUtc: _timeProvider.GetUtcNow().Add(ttl),
            Jti: jti);

        var payloadJson = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);
        var payloadB64 = Base64UrlEncode(payloadJson);
        var signaturePayload = $"{Version}.{payloadB64}";
        var signature = HmacSign(signaturePayload);
        var token = $"{signaturePayload}.{Base64UrlEncode(signature)}";

        // Seed the budget tracker so concurrent callbacks for this nonce race
        // against the same counter.
        _budget[jti] = maxCallbacks;

        return token;
    }

    /// <summary>
    /// Validates signature + expiry, returning the decoded payload on success.
    /// Does NOT consume budget — callers must invoke
    /// <see cref="TryConsumeBudget"/> separately so the budget decrement
    /// happens at the right point in the request pipeline.
    /// </summary>
    public bool TryValidate(string? nonce, out CodeActCallbackPayload? payload)
        => Validate(nonce, out payload, out _) == NonceValidationResult.Valid;

    /// <summary>
    /// Like <see cref="TryValidate"/> but returns an enum reason for failure
    /// so the callback endpoint can surface specific 401 diagnostics. Don't
    /// return raw signature comparisons to the caller — the enum buckets give
    /// enough info to debug without leaking key material.
    /// </summary>
    public NonceValidationResult Validate(
        string? nonce,
        out CodeActCallbackPayload? payload,
        out int signatureKeyByteLength)
    {
        payload = null;
        signatureKeyByteLength = 0;
        if (string.IsNullOrWhiteSpace(nonce))
        {
            return NonceValidationResult.MissingOrEmpty;
        }

        var parts = nonce.Split('.', 3);
        if (parts.Length != 3)
        {
            return NonceValidationResult.MalformedSegmentCount;
        }
        if (!string.Equals(parts[0], Version, StringComparison.Ordinal))
        {
            return NonceValidationResult.WrongVersionPrefix;
        }

        byte[] payloadBytes;
        byte[] presentedSignature;
        try
        {
            payloadBytes = Base64UrlDecode(parts[1]);
        }
        catch (FormatException)
        {
            return NonceValidationResult.PayloadNotBase64Url;
        }
        try
        {
            presentedSignature = Base64UrlDecode(parts[2]);
        }
        catch (FormatException)
        {
            return NonceValidationResult.SignatureNotBase64Url;
        }

        byte[] expectedSignature;
        try
        {
            expectedSignature = HmacSign($"{parts[0]}.{parts[1]}");
            signatureKeyByteLength = _signingKey.Value.Length;
        }
        catch (Exception)
        {
            return NonceValidationResult.SigningKeyUnavailable;
        }

        if (!CryptographicOperations.FixedTimeEquals(expectedSignature, presentedSignature))
        {
            return NonceValidationResult.SignatureMismatch;
        }

        CodeActCallbackPayload? decoded;
        try
        {
            decoded = JsonSerializer.Deserialize<CodeActCallbackPayload>(payloadBytes, JsonOptions);
        }
        catch (JsonException)
        {
            return NonceValidationResult.PayloadNotJson;
        }

        if (decoded is null)
        {
            return NonceValidationResult.PayloadEmpty;
        }

        if (decoded.ExpiresAtUtc <= _timeProvider.GetUtcNow())
        {
            _budget.TryRemove(decoded.Jti, out _);
            return NonceValidationResult.Expired;
        }

        payload = decoded;
        return NonceValidationResult.Valid;
    }

    /// <summary>
    /// Atomically decrements the per-nonce callback budget. Returns
    /// <c>false</c> when the budget is exhausted (caller should respond with
    /// HTTP 429) or when the nonce isn't tracked (caller should respond with
    /// HTTP 401 since this only happens on replays of an expired nonce after
    /// budget GC).
    /// </summary>
    public bool TryConsumeBudget(string jti)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jti);

        while (true)
        {
            if (!_budget.TryGetValue(jti, out var current))
            {
                return false;
            }
            if (current <= 0)
            {
                return false;
            }
            if (_budget.TryUpdate(jti, current - 1, current))
            {
                return true;
            }
            // Lost the race — retry.
        }
    }

    /// <summary>
    /// Returns the remaining callback budget for diagnostics. Returns 0 when
    /// the nonce isn't tracked (already expired / GC'd) so callers don't get
    /// a misleading "infinite budget" signal.
    /// </summary>
    public int PeekBudget(string jti) => _budget.TryGetValue(jti, out var current) ? current : 0;

    private byte[] HmacSign(string data)
    {
        using var hmac = new HMACSHA256(_signingKey.Value);
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
    }

    private static byte[] DecodeKey(string raw)
    {
        // Accept hex or base64 to keep operators flexible. GitHub Actions
        // secrets are particularly prone to picking up trailing whitespace
        // from copy-paste in the UI, so we trim aggressively before parsing
        // — neither hex nor base64 has any legitimate use for surrounding
        // whitespace, so trimming is always safe.
        var trimmed = raw.Trim();

        try
        {
            return Convert.FromHexString(trimmed);
        }
        catch (FormatException)
        {
            // Fall through to base64.
        }

        try
        {
            return Convert.FromBase64String(trimmed);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException(
                $"Configuration value '{ConfigKey}' must be hex or base64 encoded " +
                $"(received {trimmed.Length} characters after trimming whitespace).");
        }
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        var s = Convert.ToBase64String(bytes);
        return s.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static byte[] Base64UrlDecode(string s)
    {
        var padded = s.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }
        return Convert.FromBase64String(padded);
    }
}
