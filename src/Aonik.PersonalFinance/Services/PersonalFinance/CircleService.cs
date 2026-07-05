using System.Security.Cryptography;
using System.Text.Json;
using Aonik.PersonalFinance.Contracts.Models;
using Aonik.PersonalFinance.Contracts.Services;
using Aonik.PersonalFinance.Entities;
using Aonik.PersonalFinance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Documents;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.EntityFrameworkCore;

namespace Aonik.PersonalFinance.Services;

internal sealed class CircleService : ICircleService, ICircleVisibility
{
    private const int InviteExpiryDays = 7; // matches Spec 020 household invites
    private const int RecentLogCount = 10;
    private const int MaxPageSize = 100; // matches the owner-side payment-log read cap

    private static readonly HashSet<string> Scopes =
        new(StringComparer.OrdinalIgnoreCase) { "all", "entities", "docsOnly" };

    private static readonly IReadOnlyDictionary<string, string> EmptyAttributes = new Dictionary<string, string>();

    private readonly PersonalFinanceDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IDocumentLinkReader _documentLinkReader;

    public CircleService(
        PersonalFinanceDbContext dbContext,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider,
        IDocumentLinkReader documentLinkReader)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
        _documentLinkReader = documentLinkReader;
    }

    // ── Grants ──────────────────────────────────────────────────────────

    public async Task<CircleGrantResponse> CreateGrantAsync(CreateCircleGrantRequest request, CancellationToken cancellationToken = default)
    {
        var (tenantId, ownerUserId) = GetContext();
        var scope = NormalizeScope(request.Scope);
        var noAmounts = scope == "docsOnly" || request.NoAmounts;

        var grant = new CircleGrant
        {
            TenantId = tenantId,
            OwnerUserId = ownerUserId,
            MemberUserId = request.MemberUserId == Guid.Empty ? null : request.MemberUserId,
            Scope = scope,
            EntityIdsJson = SerializeIds(request.EntityIds),
            NoAmounts = noAmounts,
            Status = request.MemberUserId == Guid.Empty ? "pending" : "active",
        };

        _dbContext.CircleGrants.Add(grant);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return MapGrant(grant);
    }

    public async Task<IReadOnlyList<CircleGrantResponse>> ListGrantsForOwnerAsync(CancellationToken cancellationToken = default)
    {
        var (tenantId, userId) = GetContext();
        var grants = await _dbContext.CircleGrants.AsNoTracking()
            .Where(g => g.TenantId == tenantId && g.OwnerUserId == userId && g.Status != "revoked")
            .OrderByDescending(g => g.CreatedAt)
            .ToListAsync(cancellationToken);
        return grants.Select(MapGrant).ToList();
    }

    public async Task<IReadOnlyList<CircleGrantResponse>> ListGrantsForMemberAsync(CancellationToken cancellationToken = default)
    {
        var (tenantId, userId) = GetContext();
        var grants = await _dbContext.CircleGrants.AsNoTracking()
            .Where(g => g.TenantId == tenantId && g.MemberUserId == userId && g.Status == "active")
            .OrderByDescending(g => g.CreatedAt)
            .ToListAsync(cancellationToken);
        return grants.Select(MapGrant).ToList();
    }

    public async Task<bool> RevokeGrantAsync(Guid grantId, CancellationToken cancellationToken = default)
    {
        var (tenantId, userId) = GetContext();
        var grant = await _dbContext.CircleGrants
            .FirstOrDefaultAsync(g => g.Id == grantId && g.TenantId == tenantId && g.OwnerUserId == userId, cancellationToken);
        if (grant is null)
        {
            return false;
        }

        grant.Status = "revoked";
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Invites ─────────────────────────────────────────────────────────

    public async Task<CircleInviteResponse> CreateInviteAsync(CreateCircleInviteRequest request, CancellationToken cancellationToken = default)
    {
        var (tenantId, ownerUserId) = GetContext();
        var scope = NormalizeScope(request.Scope);

        var invite = new CircleInvite
        {
            TenantId = tenantId,
            OwnerUserId = ownerUserId,
            Token = GenerateToken(),
            Scope = scope,
            EntityIdsJson = SerializeIds(request.EntityIds),
            NoAmounts = scope == "docsOnly" || request.NoAmounts,
            Channel = Clean(request.Channel),
            ExpiresAt = DateTime.UtcNow.AddDays(InviteExpiryDays),
            Status = "pending",
        };

        _dbContext.CircleInvites.Add(invite);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return MapInvite(invite);
    }

    public async Task<CircleGrantResponse?> AcceptInviteAsync(string token, CancellationToken cancellationToken = default)
    {
        var (tenantId, memberUserId) = GetContext();

        var invite = await _dbContext.CircleInvites
            .FirstOrDefaultAsync(i => i.TenantId == tenantId && i.Token == token, cancellationToken);
        if (invite is null || invite.Status != "pending")
        {
            return null;
        }

        if (invite.ExpiresAt < DateTime.UtcNow)
        {
            invite.Status = "expired";
            await _dbContext.SaveChangesAsync(cancellationToken);
            return null;
        }

        var grant = new CircleGrant
        {
            TenantId = tenantId,
            OwnerUserId = invite.OwnerUserId,
            MemberUserId = memberUserId,
            Scope = invite.Scope,
            EntityIdsJson = invite.EntityIdsJson,
            NoAmounts = invite.NoAmounts,
            Status = "active",
        };
        _dbContext.CircleGrants.Add(grant);

        // Consume the invite and create the grant in ONE save so they commit together: a crash
        // between two separate saves would otherwise leave the grant created but the token still
        // "pending" (reusable). The RowVersion concurrency token on the invite makes two overlapping
        // accepts conflict — the loser's save throws rather than minting a second grant.
        invite.Status = "accepted";
        invite.ConsumedAt = DateTime.UtcNow;
        invite.GrantId = grant.Id;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapGrant(grant);
    }

    // ── Visibility filter ───────────────────────────────────────────────

    public async Task<CircleGrantView?> ResolveAsync(Guid ownerUserId, CancellationToken cancellationToken = default)
    {
        var grants = await GetActiveGrantsAsync(ownerUserId, cancellationToken);
        if (grants.Count == 0)
        {
            return null; // fail closed: no active grant → no access
        }

        // A member may hold several active grants for one owner (a direct grant plus an
        // invite-accepted one, or shares of different entities). Merge them so nothing is missed:
        // most-permissive scope, union of entity ids, amounts shown if any grant shows them. This
        // merged view drives listing + the general access check; the per-entity amount decision is
        // made in GetSharedEntityAsync so a docsOnly entity never inherits another grant's amounts.
        var scope = grants.Any(g => g.Scope == "all") ? "all"
            : grants.Any(g => g.Scope == "entities") ? "entities"
            : "docsOnly";
        var entityIds = grants.SelectMany(g => g.EntityIds).Distinct().ToList();
        var noAmounts = grants.All(g => g.NoAmounts);

        return new CircleGrantView(ownerUserId, scope, entityIds, noAmounts);
    }

    /// <summary>All active, known-scope grants for (current member → owner). Fail-closed: unknown scopes dropped.</summary>
    private async Task<IReadOnlyList<CircleGrantView>> GetActiveGrantsAsync(Guid ownerUserId, CancellationToken cancellationToken)
    {
        var (tenantId, memberUserId) = GetContext();
        var grants = await _dbContext.CircleGrants.AsNoTracking()
            .Where(g => g.TenantId == tenantId && g.OwnerUserId == ownerUserId
                && g.MemberUserId == memberUserId && g.Status == "active")
            .ToListAsync(cancellationToken);

        return grants
            .Where(g => Scopes.Contains(g.Scope))
            .Select(g => new CircleGrantView(g.OwnerUserId, g.Scope, ParseIds(g.EntityIdsJson), g.NoAmounts))
            .ToList();
    }

    // ── Shared reads (member viewing an owner's data) ───────────────────

    public async Task<IReadOnlyList<CareEntityRef>?> ListSharedEntitiesAsync(Guid ownerUserId, CancellationToken cancellationToken = default)
    {
        var grant = await ResolveAsync(ownerUserId, cancellationToken);
        if (grant is null)
        {
            return null;
        }

        var (tenantId, _) = GetContext();
        var query = _dbContext.CareEntities.AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.UserId == ownerUserId && !e.Archived);

        if (grant.Scope != "all")
        {
            var ids = grant.EntityIds;
            query = query.Where(e => ids.Contains(e.Id));
        }

        var entities = await query.OrderBy(e => e.Name).ToListAsync(cancellationToken);
        return entities.Select(e => new CareEntityRef(e.Id, e.Name, e.Kind, e.CountryCode)).ToList();
    }

    public async Task<CircleSharedEntityResult?> GetSharedEntityAsync(Guid ownerUserId, Guid careEntityId, CancellationToken cancellationToken = default)
    {
        // Resolve PER ENTITY across all the member's active grants — find every grant that covers
        // this entity, then take the most-permissive of them. This avoids both under-reporting
        // (a later grant being ignored) and over-reporting (a docsOnly entity inheriting amounts
        // from an unrelated entities/all grant).
        var grants = await GetActiveGrantsAsync(ownerUserId, cancellationToken);
        var covering = grants.Where(g => g.Scope == "all" || g.EntityIds.Contains(careEntityId)).ToList();
        if (covering.Count == 0)
        {
            return null; // no grant covers this entity → not found
        }

        var (tenantId, _) = GetContext();
        var entity = await _dbContext.CareEntities.AsNoTracking()
            .FirstOrDefaultAsync(
                e => e.Id == careEntityId && e.TenantId == tenantId && e.UserId == ownerUserId && !e.Archived,
                cancellationToken);
        if (entity is null)
        {
            return null;
        }

        // The owner's linked document refs (refs only — no bytes, no amounts), read by the
        // owner's identity since the member can't see them through their own scope. The grant
        // above has already authorised this member to see this entity.
        var documents = await GetOwnerDocumentsAsync(ownerUserId, careEntityId, cancellationToken);

        // Only docsOnly grants cover this entity → the structurally amount-free projection.
        var hasFullScope = covering.Any(g => g.Scope == "all" || g.Scope == "entities");
        if (!hasFullScope)
        {
            return new CircleSharedEntityResult(
                "docsOnly",
                Full: null,
                DocsOnly: new CircleSharedDocsView(entity.Id, entity.Name, documents));
        }

        // Full view. Amounts are shown only if a covering grant permits them (NoAmounts=false);
        // otherwise the money-bearing fields are suppressed and the logs are not even read.
        var amountsAllowed = covering.Any(g => !g.NoAmounts);
        IReadOnlyList<CurrencyTotal> yearTotals = [];
        IReadOnlyList<CareEntityPaymentLogSummary> recentLogs = [];

        if (amountsAllowed)
        {
            var logs = await _dbContext.PaymentLogs.AsNoTracking()
                .Where(p => p.TenantId == tenantId && p.UserId == ownerUserId && p.CareEntityId == careEntityId)
                .ToListAsync(cancellationToken);

            yearTotals = logs
                .GroupBy(p => p.Currency)
                .Select(g => new CurrencyTotal(g.Key, g.Sum(p => p.Amount), g.Count()))
                .OrderBy(t => t.Currency)
                .ToList();

            recentLogs = logs
                .OrderByDescending(p => p.Date).ThenByDescending(p => p.CreatedAt)
                .Take(RecentLogCount)
                .Select(p => new CareEntityPaymentLogSummary(p.Id, p.Amount, p.Currency, p.Date, p.Channel, p.CorroborationStatus))
                .ToList();
        }

        var effectiveScope = covering.Any(g => g.Scope == "all") ? "all" : "entities";
        return new CircleSharedEntityResult(
            effectiveScope,
            Full: new CircleSharedEntityView(MapEntity(entity), yearTotals, recentLogs, documents),
            DocsOnly: null);
    }

    public async Task<CircleSharedPaymentLogsResult?> GetSharedPaymentLogsAsync(
        Guid ownerUserId, Guid careEntityId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        // Same per-entity authorisation as GetSharedEntityAsync — find every active grant covering
        // this entity. No covering grant → not found (existence not revealed).
        var grants = await GetActiveGrantsAsync(ownerUserId, cancellationToken);
        var covering = grants.Where(g => g.Scope == "all" || g.EntityIds.Contains(careEntityId)).ToList();
        if (covering.Count == 0)
        {
            return null;
        }

        // Expense lines are money: shown only when a covering grant carries full scope AND permits
        // amounts. A docsOnly / NoAmounts member gets 404 here — the same gate as the entity view's
        // amountsAllowed branch, so the no-amounts property holds and spend is never revealed.
        var amountsAllowed = covering.Any(g => g.Scope == "all" || g.Scope == "entities")
            && covering.Any(g => !g.NoAmounts);
        if (!amountsAllowed)
        {
            return null;
        }

        var (tenantId, _) = GetContext();

        // The entity must still belong to the owner and be live — mirrors the entity view's check.
        var entityExists = await _dbContext.CareEntities.AsNoTracking().AnyAsync(
            e => e.Id == careEntityId && e.TenantId == tenantId && e.UserId == ownerUserId && !e.Archived,
            cancellationToken);
        if (!entityExists)
        {
            return null;
        }

        var size = Math.Clamp(pageSize, 1, MaxPageSize);
        var pageNumber = Math.Max(page, 1);

        // Same ordering as the recent-log preview (newest first). Fetch one extra row to derive
        // HasMore without a second COUNT round-trip.
        var rows = await _dbContext.PaymentLogs.AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.UserId == ownerUserId && p.CareEntityId == careEntityId)
            .OrderByDescending(p => p.Date).ThenByDescending(p => p.CreatedAt)
            .Skip((pageNumber - 1) * size)
            .Take(size + 1)
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > size;
        var items = rows
            .Take(size)
            .Select(p => new CareEntityPaymentLogSummary(p.Id, p.Amount, p.Currency, p.Date, p.Channel, p.CorroborationStatus))
            .ToList();

        return new CircleSharedPaymentLogsResult(items, pageNumber, size, hasMore);
    }

    /// <summary>
    /// The owner's linked document refs for an entity, read by the owner's identity (Spec 046
    /// cross-module owner-read). Refs only — no amounts — so the docsOnly guarantee holds.
    /// Callers must have authorised the member via the grant first.
    /// </summary>
    private async Task<IReadOnlyList<CareEntityDocumentRef>> GetOwnerDocumentsAsync(
        Guid ownerUserId, Guid careEntityId, CancellationToken cancellationToken)
    {
        var refs = await _documentLinkReader.GetForOwnerTargetAsync(
            ownerUserId, "careEntity", careEntityId, cancellationToken);
        return refs
            .Select(d => new CareEntityDocumentRef(d.DocumentId, d.Title, d.DocumentType))
            .ToList();
    }

    // ── helpers ─────────────────────────────────────────────────────────

    private (Guid TenantId, Guid UserId) GetContext()
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        if (!_currentUserProvider.TryGetCurrentUserId(out var userId))
        {
            throw new InvalidOperationException("Authenticated user is required.");
        }

        return (tenantId, userId);
    }

    private static string NormalizeScope(string? scope)
    {
        var value = (scope ?? string.Empty).Trim();
        return Scopes.Contains(value)
            ? (value.Equals("docsonly", StringComparison.OrdinalIgnoreCase) ? "docsOnly" : value.ToLowerInvariant())
            : throw new ArgumentException($"Scope must be one of: {string.Join(", ", Scopes)}.", nameof(scope));
    }

    private static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    private static string SerializeIds(IReadOnlyList<Guid>? ids)
        => ids is null || ids.Count == 0 ? "[]" : JsonSerializer.Serialize(ids);

    private static IReadOnlyList<Guid> ParseIds(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "[]")
        {
            return Array.Empty<Guid>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<Guid>>(json) ?? new List<Guid>();
        }
        catch (JsonException)
        {
            return Array.Empty<Guid>();
        }
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static CircleGrantResponse MapGrant(CircleGrant g)
        => new(g.Id, g.OwnerUserId, g.MemberUserId, g.Scope, ParseIds(g.EntityIdsJson), g.NoAmounts, g.Status, g.CreatedAt);

    private static CircleInviteResponse MapInvite(CircleInvite i)
        => new(i.Id, i.Token, i.Scope, ParseIds(i.EntityIdsJson), i.NoAmounts, i.Channel, i.ExpiresAt, i.Status);

    private static CareEntityResponse MapEntity(CareEntity e)
        // PhotoUrl is null here: the circle (docs-only) list view omits the resolved banner URL to
        // avoid an N+1 over Documents, matching CareEntityService.ListAsync (Spec 049 §9).
        => new(
            e.Id, e.Kind, e.AssetType, e.Name, e.CountryCode, e.Relationship, e.Emoji, e.PhotoDocumentId,
            null, ParseAttributes(e.AttributesJson), e.Archived, e.CreatedAt, e.UpdatedAt);

    private static IReadOnlyDictionary<string, string> ParseAttributes(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "{}")
        {
            return EmptyAttributes;
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
        }
        catch (JsonException)
        {
            return EmptyAttributes;
        }
    }
}
