using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Observability;
using Aonik.Platform.Contracts.Services.Storage;

namespace Aonik.Platform.Mcp.Hosting;

/// <summary>
/// Lightweight implementations of cross-cutting abstractions for the
/// Platform MCP server process. These replace the HttpContext-based
/// implementations from Infrastructure that are not available in a
/// headless console host.
/// </summary>

// ── Clock ────────────────────────────────────────────────────────────

internal sealed class McpSystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}

// ── Tenant Provider ──────────────────────────────────────────────────

internal sealed class McpTenantProvider : ITenantProvider
{
    private readonly Guid _tenantId;

    public McpTenantProvider(Guid tenantId) => _tenantId = tenantId;

    public Guid GetCurrentTenantId() => _tenantId;

    public bool TryGetCurrentTenantId(out Guid tenantId)
    {
        tenantId = _tenantId;
        return true;
    }
}

// ── Tenant Context ───────────────────────────────────────────────────

internal sealed class McpTenantContext : ITenantContext
{
    public McpTenantContext(Guid tenantId)
    {
        TenantId = tenantId;
        ResolutionSource = "MCP";
    }

    public Guid? TenantId { get; set; }
    public string? ResolutionSource { get; set; }
    public bool IsResolved => TenantId.HasValue;
}

// ── Current User Provider ────────────────────────────────────────────

internal sealed class McpCurrentUserProvider : ICurrentUserProvider
{
    private readonly Guid _userId;

    public McpCurrentUserProvider(Guid userId) => _userId = userId;

    public Guid? GetCurrentUserId() => _userId;

    public bool TryGetCurrentUserId(out Guid userId)
    {
        userId = _userId;
        return true;
    }
}

// ── Current User Context ─────────────────────────────────────────────

internal sealed class McpCurrentUserContext : ICurrentUserContext
{
    public McpCurrentUserContext(Guid userId, Guid tenantId)
    {
        UserId = userId;
        TenantId = tenantId;
        Roles = new[] { "PlatformAdmin" };
        IsAuthenticated = true;
    }

    public Guid? UserId { get; set; }
    public Guid? TenantId { get; set; }
    public string? ExternalIssuer { get; set; }
    public string? ExternalSubject { get; set; }
    public IReadOnlyCollection<string> Roles { get; set; }
    public bool IsAuthenticated { get; set; }
}

// ── Correlation Context ──────────────────────────────────────────────

internal sealed class McpCorrelationContext : ICorrelationContext
{
    public string? CorrelationId { get; } = $"mcp-{Guid.NewGuid():N}";
}

// ── Currency Metadata Provider ───────────────────────────────────────

internal sealed class McpCurrencyMetadataProvider : ICurrencyMetadataProvider
{
    private static readonly Dictionary<string, CurrencyMetadata> Currencies = new(StringComparer.OrdinalIgnoreCase)
    {
        ["USD"] = new("USD", 2),
        ["NGN"] = new("NGN", 2),
        ["GBP"] = new("GBP", 2),
        ["EUR"] = new("EUR", 2),
        ["KES"] = new("KES", 2),
        ["ZAR"] = new("ZAR", 2),
    };

    public bool TryGetCurrency(string currency, out CurrencyMetadata metadata)
        => Currencies.TryGetValue(currency, out metadata!);

    public CurrencyMetadata GetCurrency(string currency)
        => Currencies.TryGetValue(currency, out var m)
            ? m
            : new CurrencyMetadata(currency, 2);
}

// ── Profile Photo Store ──────────────────────────────────────────────

/// <summary>
/// No-op photo store for MCP context. Photo operations are not supported
/// via MCP tools — they require file streams.
/// </summary>
internal sealed class McpProfilePhotoStore : IProfilePhotoStore
{
    public Task<PhotoUploadResult> UploadCustomerPhotoAsync(
        Guid tenantId, Guid partyId, string contentType, Stream fileStream,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Photo upload is not available via MCP.");

    public Task DeleteCustomerPhotoAsync(string photoUrl, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Photo deletion is not available via MCP.");

    public string GetPhotoUrl(string blobPath) => blobPath;
}

// ── Document File Store ──────────────────────────────────────────────

/// <summary>
/// No-op document file store for MCP context. File uploads are not supported
/// via MCP tools — they require file streams.
/// </summary>
internal sealed class McpDocumentFileStore : Aonik.SharedKernel.Abstractions.Documents.IDocumentFileStore
{
    public Task<Aonik.SharedKernel.Abstractions.Documents.DocumentFileUploadResult> UploadDocumentFileAsync(
        Guid tenantId, Guid documentId, Stream fileStream, string fileName,
        string contentType, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Document file upload is not available via MCP.");

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Document file read is not available via MCP.");

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Document file deletion is not available via MCP.");
}
