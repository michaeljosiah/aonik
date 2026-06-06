using Aonik.Agents.Contracts.Models.Tenant;
using Aonik.Agents.Entities;
using Aonik.Agents.Framework;
using Aonik.Agents.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Agents.Services.Tenant;

/// <summary>
/// Tenant declarative HTTP/OpenAPI tool management (Spec 033 §8.4, §7.1). Persists tools with
/// write-only encrypted credentials, runs the review state machine, and reports the tier the gate
/// would assign. A tenant cannot lower the tier below High; only a PlatformAdmin review may.
/// </summary>
public interface ITenantHttpToolService
{
    Task<IReadOnlyList<TenantHttpToolDto>> ListAsync(CancellationToken ct = default);
    Task<TenantHttpToolDto?> GetAsync(Guid id, CancellationToken ct = default);
    Task<TenantHttpToolDto> CreateAsync(SaveHttpToolRequest request, CancellationToken ct = default);
    Task<TenantHttpToolDto?> UpdateAsync(Guid id, SaveHttpToolRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<TenantHttpToolDto?> SubmitAsync(Guid id, CancellationToken ct = default);
    Task<TenantHttpToolDto?> ActivateAsync(Guid id, CancellationToken ct = default);
    Task<TenantHttpToolDto?> DeactivateAsync(Guid id, CancellationToken ct = default);
    Task<HttpToolTestDto?> TestAsync(Guid id, CancellationToken ct = default);
    Task<TenantHttpToolDto?> ReviewAsync(Guid id, ReviewHttpToolRequest request, CancellationToken ct = default);
}

internal sealed class TenantHttpToolService : ITenantHttpToolService
{
    private readonly AgentsDbContext _db;
    private readonly ITenantProvider _tenant;
    private readonly ICurrentUserProvider _user;
    private readonly ITenantCredentialProtector _protector;
    private readonly ITenantEgressAllowList _egress;
    private readonly IClock _clock;

    public TenantHttpToolService(
        AgentsDbContext db,
        ITenantProvider tenant,
        ICurrentUserProvider user,
        ITenantCredentialProtector protector,
        ITenantEgressAllowList egress,
        IClock clock)
    {
        _db = db;
        _tenant = tenant;
        _user = user;
        _protector = protector;
        _egress = egress;
        _clock = clock;
    }

    public async Task<IReadOnlyList<TenantHttpToolDto>> ListAsync(CancellationToken ct = default)
    {
        var rows = await _db.TenantHttpTools.AsNoTracking()
            .OrderByDescending(t => t.CreatedAt).ToListAsync(ct).ConfigureAwait(false);
        return rows.Select(ToDto).ToList();
    }

    public async Task<TenantHttpToolDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var row = await _db.TenantHttpTools.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct).ConfigureAwait(false);
        return row is null ? null : ToDto(row);
    }

    public async Task<TenantHttpToolDto> CreateAsync(SaveHttpToolRequest request, CancellationToken ct = default)
    {
        var tenantId = RequireTenant();
        var authKind = ParseEnum(request.AuthKind, TenantToolAuthKind.None);
        var method = NormalizeMethod(request.Method);

        var tool = new TenantHttpTool
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = request.Name.Trim(),
            Description = request.Description.Trim(),
            Method = method,
            UrlTemplate = request.UrlTemplate.Trim(),
            ParameterSchemaJson = string.IsNullOrWhiteSpace(request.ParameterSchemaJson) ? "{}" : request.ParameterSchemaJson,
            AuthKind = authKind,
            ProtectedAuthJson = Protect(authKind, request),
            // Tenant default: High for any non-GET; a side-effect-free GET stays High until a
            // PlatformAdmin reclassifies it ReadOnly. A tenant can never self-lower.
            RiskTier = TenantToolRiskTier.High,
            ActionKind = request.Name.Trim(),
            ApprovalState = TenantExtensionApprovalState.Draft,
            IsActive = false,
            CredentialVersion = 1,
        };
        _db.TenantHttpTools.Add(tool);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return ToDto(tool);
    }

    public async Task<TenantHttpToolDto?> UpdateAsync(Guid id, SaveHttpToolRequest request, CancellationToken ct = default)
    {
        var tool = await _db.TenantHttpTools.FirstOrDefaultAsync(t => t.Id == id, ct).ConfigureAwait(false);
        if (tool is null)
        {
            return null;
        }

        var authKind = ParseEnum(request.AuthKind, TenantToolAuthKind.None);
        tool.Name = request.Name.Trim();
        tool.Description = request.Description.Trim();
        tool.Method = NormalizeMethod(request.Method);
        tool.UrlTemplate = request.UrlTemplate.Trim();
        tool.ParameterSchemaJson = string.IsNullOrWhiteSpace(request.ParameterSchemaJson) ? "{}" : request.ParameterSchemaJson;
        tool.AuthKind = authKind;

        var newAuth = Protect(authKind, request);
        if (newAuth is not null || authKind == TenantToolAuthKind.None)
        {
            tool.ProtectedAuthJson = newAuth;
        }

        // Edit invalidates the prior review and resets the tier to the safe default — a PlatformAdmin
        // must re-review to lower it again.
        tool.RiskTier = TenantToolRiskTier.High;
        tool.ApprovalState = TenantExtensionApprovalState.Draft;
        tool.IsActive = false;
        tool.CredentialVersion += 1;

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return ToDto(tool);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var tool = await _db.TenantHttpTools.FirstOrDefaultAsync(t => t.Id == id, ct).ConfigureAwait(false);
        if (tool is null)
        {
            return false;
        }
        _db.TenantHttpTools.Remove(tool);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return true;
    }

    public Task<TenantHttpToolDto?> SubmitAsync(Guid id, CancellationToken ct = default) =>
        MutateAsync(id, t =>
        {
            if (t.ApprovalState is not (TenantExtensionApprovalState.Draft or TenantExtensionApprovalState.Rejected))
            {
                throw new InvalidOperationException("Only a draft or rejected tool can be submitted for review.");
            }
            if (!_egress.IsAllowed(t.UrlTemplate, out var reason))
            {
                throw new InvalidOperationException(reason ?? "URL host is not on the egress allow-list.");
            }
            t.ApprovalState = TenantExtensionApprovalState.PendingPlatformReview;
        }, ct);

    public Task<TenantHttpToolDto?> ActivateAsync(Guid id, CancellationToken ct = default) =>
        MutateAsync(id, t =>
        {
            if (t.ApprovalState != TenantExtensionApprovalState.Approved)
            {
                throw new InvalidOperationException("Only an approved tool can be activated.");
            }
            t.IsActive = true;
        }, ct);

    public Task<TenantHttpToolDto?> DeactivateAsync(Guid id, CancellationToken ct = default) =>
        MutateAsync(id, t => t.IsActive = false, ct);

    public async Task<HttpToolTestDto?> TestAsync(Guid id, CancellationToken ct = default)
    {
        var tool = await _db.TenantHttpTools.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct).ConfigureAwait(false);
        if (tool is null)
        {
            return null;
        }

        var classification = TenantToolRiskMapping.ClassifyHttpTool(tool);
        var tier = classification.IsReadOnly ? "ReadOnly" : classification.Options!.Tier.ToString();
        var note = classification.IsReadOnly
            ? "Read-only — executes directly when invoked."
            : $"{tier} — gated; the agent surfaces a requires-approval / queued result instead of running in-band.";

        return new HttpToolTestDto(tool.Name, tier, tool.ParameterSchemaJson, note);
    }

    public Task<TenantHttpToolDto?> ReviewAsync(Guid id, ReviewHttpToolRequest request, CancellationToken ct = default) =>
        MutateAsync(id, t =>
        {
            if (request.Approve && !_egress.IsAllowed(t.UrlTemplate, out var reason))
            {
                throw new InvalidOperationException(reason ?? "URL host is not on the egress allow-list.");
            }
            if (!string.IsNullOrWhiteSpace(request.RiskTier)
                && Enum.TryParse<TenantToolRiskTier>(request.RiskTier, ignoreCase: true, out var tier))
            {
                t.RiskTier = tier;
            }
            if (!string.IsNullOrWhiteSpace(request.ActionKind))
            {
                t.ActionKind = request.ActionKind!;
            }
            if (request.ProposalType is not null)
            {
                t.ProposalType = request.ProposalType;
            }
            t.ApprovalState = request.Approve ? TenantExtensionApprovalState.Approved : TenantExtensionApprovalState.Rejected;
            t.ReviewedByUserId = CurrentUserId();
            t.ReviewedAt = _clock.UtcNow;
            t.ReviewNotes = request.Notes;
            if (!request.Approve)
            {
                t.IsActive = false;
            }
        }, ct);

    private async Task<TenantHttpToolDto?> MutateAsync(Guid id, Action<TenantHttpTool> mutate, CancellationToken ct)
    {
        var tool = await _db.TenantHttpTools.FirstOrDefaultAsync(t => t.Id == id, ct).ConfigureAwait(false);
        if (tool is null)
        {
            return null;
        }
        mutate(tool);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return ToDto(tool);
    }

    private string? Protect(TenantToolAuthKind kind, SaveHttpToolRequest request)
    {
        var json = TenantRemoteAuth.BuildAuthJson(kind, request.AuthSecret, request.AuthUsername, request.AuthHeaderName);
        return json is null ? null : _protector.Protect(json);
    }

    private Guid RequireTenant() =>
        _tenant.TryGetCurrentTenantId(out var id) && id != Guid.Empty
            ? id
            : throw new InvalidOperationException("A tenant context is required.");

    private Guid? CurrentUserId() => _user.TryGetCurrentUserId(out var id) ? id : null;

    private static string NormalizeMethod(string? method) =>
        string.IsNullOrWhiteSpace(method) ? "GET" : method.Trim().ToUpperInvariant();

    private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback) where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) ? parsed : fallback;

    private static TenantHttpToolDto ToDto(TenantHttpTool t) => new(
        t.Id, t.Name, t.Description, t.Method, t.UrlTemplate, t.ParameterSchemaJson,
        t.AuthKind.ToString(), AuthConfigured: !string.IsNullOrWhiteSpace(t.ProtectedAuthJson),
        t.RiskTier.ToString(), t.ActionKind, t.ApprovalState.ToString(), t.IsActive,
        t.CreatedAt, t.ReviewedAt, t.ReviewNotes);
}
