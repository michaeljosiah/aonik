using System.Text.Json;
using Aonik.Agents.Contracts.Models.Tenant;
using Aonik.Agents.Entities;
using Aonik.Agents.Framework;
using Aonik.Agents.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;

namespace Aonik.Agents.Services.Tenant;

/// <summary>
/// Tenant remote MCP server management (Spec 033 §8.3, §7.1). Persists servers with write-only
/// encrypted credentials, runs the review state machine, and offers a dry-run connect that lists the
/// server's tools and the tier the gate would assign — all without activating the server.
/// </summary>
public interface ITenantMcpServerService
{
    Task<IReadOnlyList<TenantMcpServerDto>> ListAsync(CancellationToken ct = default);
    Task<TenantMcpServerDto?> GetAsync(Guid id, CancellationToken ct = default);
    Task<TenantMcpServerDto> CreateAsync(SaveMcpServerRequest request, CancellationToken ct = default);
    Task<TenantMcpServerDto?> UpdateAsync(Guid id, SaveMcpServerRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<TenantMcpServerDto?> SubmitAsync(Guid id, CancellationToken ct = default);
    Task<TenantMcpServerDto?> ActivateAsync(Guid id, CancellationToken ct = default);
    Task<TenantMcpServerDto?> DeactivateAsync(Guid id, CancellationToken ct = default);
    Task<McpDryRunDto> DryRunAsync(Guid id, CancellationToken ct = default);
    Task<TenantMcpServerDto?> ReviewAsync(Guid id, ReviewMcpServerRequest request, CancellationToken ct = default);
}

internal sealed class TenantMcpServerService : ITenantMcpServerService
{
    private static readonly TimeSpan DryRunTimeout = TimeSpan.FromSeconds(20);

    private readonly AgentsDbContext _db;
    private readonly ITenantProvider _tenant;
    private readonly ICurrentUserProvider _user;
    private readonly ITenantCredentialProtector _protector;
    private readonly ITenantEgressAllowList _egress;
    private readonly TenantMcpConnectionCache _cache;
    private readonly IClock _clock;

    public TenantMcpServerService(
        AgentsDbContext db,
        ITenantProvider tenant,
        ICurrentUserProvider user,
        ITenantCredentialProtector protector,
        ITenantEgressAllowList egress,
        TenantMcpConnectionCache cache,
        IClock clock)
    {
        _db = db;
        _tenant = tenant;
        _user = user;
        _protector = protector;
        _egress = egress;
        _cache = cache;
        _clock = clock;
    }

    public async Task<IReadOnlyList<TenantMcpServerDto>> ListAsync(CancellationToken ct = default)
    {
        var rows = await _db.TenantMcpServers.AsNoTracking()
            .OrderByDescending(s => s.CreatedAt).ToListAsync(ct).ConfigureAwait(false);
        return rows.Select(ToDto).ToList();
    }

    public async Task<TenantMcpServerDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var row = await _db.TenantMcpServers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct).ConfigureAwait(false);
        return row is null ? null : ToDto(row);
    }

    public async Task<TenantMcpServerDto> CreateAsync(SaveMcpServerRequest request, CancellationToken ct = default)
    {
        var tenantId = RequireTenant();
        var authKind = ParseEnum(request.AuthKind, TenantToolAuthKind.None);
        var server = new TenantMcpServer
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = request.Name.Trim(),
            Endpoint = request.Endpoint.Trim(),
            TransportType = ParseEnum(request.TransportType, TenantMcpTransportType.Http),
            AuthKind = authKind,
            ProtectedAuthJson = Protect(authKind, request),
            AllowedToolPrefixesJson = JsonSerializer.Serialize(request.AllowedToolPrefixes ?? []),
            DefaultRiskTier = TenantToolRiskTier.High,
            ApprovalState = TenantExtensionApprovalState.Draft,
            IsActive = false,
            CredentialVersion = 1,
        };
        _db.TenantMcpServers.Add(server);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return ToDto(server);
    }

    public async Task<TenantMcpServerDto?> UpdateAsync(Guid id, SaveMcpServerRequest request, CancellationToken ct = default)
    {
        var server = await _db.TenantMcpServers.FirstOrDefaultAsync(s => s.Id == id, ct).ConfigureAwait(false);
        if (server is null)
        {
            return null;
        }

        var authKind = ParseEnum(request.AuthKind, TenantToolAuthKind.None);
        server.Name = request.Name.Trim();
        server.Endpoint = request.Endpoint.Trim();
        server.TransportType = ParseEnum(request.TransportType, TenantMcpTransportType.Http);
        server.AuthKind = authKind;
        server.AllowedToolPrefixesJson = JsonSerializer.Serialize(request.AllowedToolPrefixes ?? []);

        // Only replace the secret when a new one is supplied; otherwise keep the stored one.
        var newAuth = Protect(authKind, request);
        if (newAuth is not null || authKind == TenantToolAuthKind.None)
        {
            server.ProtectedAuthJson = newAuth;
        }

        // An edit can change behaviour, so prior approval no longer holds: return to Draft, deactivate,
        // bump the credential version (invalidates the connection cache).
        server.ApprovalState = TenantExtensionApprovalState.Draft;
        server.IsActive = false;
        server.CredentialVersion += 1;

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        await _cache.InvalidateAsync(server.TenantId, server.Id).ConfigureAwait(false);
        return ToDto(server);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var server = await _db.TenantMcpServers.FirstOrDefaultAsync(s => s.Id == id, ct).ConfigureAwait(false);
        if (server is null)
        {
            return false;
        }
        _db.TenantMcpServers.Remove(server);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        await _cache.InvalidateAsync(server.TenantId, server.Id).ConfigureAwait(false);
        return true;
    }

    public Task<TenantMcpServerDto?> SubmitAsync(Guid id, CancellationToken ct = default) =>
        MutateAsync(id, s =>
        {
            if (s.ApprovalState is not (TenantExtensionApprovalState.Draft or TenantExtensionApprovalState.Rejected))
            {
                throw new InvalidOperationException("Only a draft or rejected server can be submitted for review.");
            }
            if (!_egress.IsAllowed(s.Endpoint, out var reason))
            {
                throw new InvalidOperationException(reason ?? "Endpoint host is not on the egress allow-list.");
            }
            s.ApprovalState = TenantExtensionApprovalState.PendingPlatformReview;
        }, ct);

    public Task<TenantMcpServerDto?> ActivateAsync(Guid id, CancellationToken ct = default) =>
        MutateAsync(id, s =>
        {
            if (s.ApprovalState != TenantExtensionApprovalState.Approved)
            {
                throw new InvalidOperationException("Only an approved server can be activated.");
            }
            s.IsActive = true;
        }, ct);

    public Task<TenantMcpServerDto?> DeactivateAsync(Guid id, CancellationToken ct = default) =>
        MutateAsync(id, s => s.IsActive = false, ct);

    public async Task<McpDryRunDto> DryRunAsync(Guid id, CancellationToken ct = default)
    {
        var server = await _db.TenantMcpServers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct).ConfigureAwait(false);
        if (server is null)
        {
            return new McpDryRunDto(false, "Server not found.", []);
        }

        if (!_egress.IsAllowed(server.Endpoint, out var reason))
        {
            return new McpDryRunDto(false, reason, []);
        }

        try
        {
            var headers = TenantRemoteAuth.BuildHeaders(server.AuthKind, server.ProtectedAuthJson, _protector);
            var options = new HttpClientTransportOptions { Name = server.Name, Endpoint = new Uri(server.Endpoint) };
            if (headers.Count > 0)
            {
                options.AdditionalHeaders = headers;
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(DryRunTimeout);

            await using var client = await McpClient.CreateAsync(new HttpClientTransport(options), cancellationToken: cts.Token).ConfigureAwait(false);
            var discovered = await client.ListToolsAsync(cancellationToken: cts.Token).ConfigureAwait(false);

            var tools = discovered.Cast<AITool>().Select(t =>
            {
                var classification = TenantToolRiskMapping.ClassifyMcpTool(t.Name, server);
                var tier = classification.IsReadOnly ? "ReadOnly" : classification.Options!.Tier.ToString();
                return new McpDiscoveredToolDto(t.Name, t.Description ?? "", tier);
            }).ToList();

            return new McpDryRunDto(true, null, tools);
        }
        catch (Exception ex)
        {
            return new McpDryRunDto(false, ex.Message, []);
        }
    }

    public Task<TenantMcpServerDto?> ReviewAsync(Guid id, ReviewMcpServerRequest request, CancellationToken ct = default) =>
        MutateAsync(id, s =>
        {
            if (request.Approve && !_egress.IsAllowed(s.Endpoint, out var reason))
            {
                throw new InvalidOperationException(reason ?? "Endpoint host is not on the egress allow-list.");
            }
            if (!string.IsNullOrWhiteSpace(request.DefaultRiskTier)
                && Enum.TryParse<TenantToolRiskTier>(request.DefaultRiskTier, ignoreCase: true, out var tier))
            {
                s.DefaultRiskTier = tier;
            }
            s.ApprovalState = request.Approve ? TenantExtensionApprovalState.Approved : TenantExtensionApprovalState.Rejected;
            s.ReviewedByUserId = CurrentUserId();
            s.ReviewedAt = _clock.UtcNow;
            s.ReviewNotes = request.Notes;
            if (!request.Approve)
            {
                s.IsActive = false;
            }
        }, ct);

    private async Task<TenantMcpServerDto?> MutateAsync(Guid id, Action<TenantMcpServer> mutate, CancellationToken ct)
    {
        var server = await _db.TenantMcpServers.FirstOrDefaultAsync(s => s.Id == id, ct).ConfigureAwait(false);
        if (server is null)
        {
            return null;
        }
        mutate(server);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return ToDto(server);
    }

    private string? Protect(TenantToolAuthKind kind, SaveMcpServerRequest request)
    {
        var json = TenantRemoteAuth.BuildAuthJson(kind, request.AuthSecret, request.AuthUsername, request.AuthHeaderName);
        return json is null ? null : _protector.Protect(json);
    }

    private Guid RequireTenant() =>
        _tenant.TryGetCurrentTenantId(out var id) && id != Guid.Empty
            ? id
            : throw new InvalidOperationException("A tenant context is required.");

    private Guid? CurrentUserId() => _user.TryGetCurrentUserId(out var id) ? id : null;

    private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback) where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) ? parsed : fallback;

    private static TenantMcpServerDto ToDto(TenantMcpServer s) => new(
        s.Id, s.Name, s.Endpoint, s.TransportType.ToString(), s.AuthKind.ToString(),
        AuthConfigured: !string.IsNullOrWhiteSpace(s.ProtectedAuthJson),
        AllowedToolPrefixes: ParsePrefixes(s.AllowedToolPrefixesJson),
        DefaultRiskTier: s.DefaultRiskTier.ToString(),
        ApprovalState: s.ApprovalState.ToString(),
        IsActive: s.IsActive,
        CredentialVersion: s.CredentialVersion,
        CreatedAt: s.CreatedAt,
        ReviewedAt: s.ReviewedAt,
        ReviewNotes: s.ReviewNotes);

    private static IReadOnlyList<string> ParsePrefixes(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch { return []; }
    }
}
