using System.Security.Cryptography;
using System.Text.Json;
using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using Aonik.Finance.Entities.PersonalFinance;
using Aonik.PersonalFinance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Documents;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Finance.Services.PersonalFinance;

internal sealed class CircleService : ICircleService, ICircleVisibility
{
    private const int InviteExpiryDays = 7; // matches Spec 020 household invites
    private const int RecentLogCount = 10;

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
        var (tenantId, memberUserId) = GetContext();
        var grant = await _dbContext.CircleGrants.AsNoTracking()
            .FirstOrDefaultAsync(
                g => g.TenantId == tenantId && g.OwnerUserId == ownerUserId
                    && g.MemberUserId == memberUserId && g.Status == "active",
                cancellationToken);
        if (grant is null || !Scopes.Contains(grant.Scope))
        {
            return null; // fail closed: no grant or unknown scope → no access
        }

        return new CircleGrantView(grant.OwnerUserId, grant.Scope, ParseIds(grant.EntityIdsJson), grant.NoAmounts);
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
        var grant = await ResolveAsync(ownerUserId, cancellationToken);
        if (grant is null)
        {
            return null;
        }

        if (grant.Scope != "all" && !grant.EntityIds.Contains(careEntityId))
        {
            return null; // out of scope → not found
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

        // docsOnly → the structurally amount-free projection (no logs/totals joined).
        if (grant.Scope == "docsOnly")
        {
            return new CircleSharedEntityResult(
                "docsOnly",
                Full: null,
                DocsOnly: new CircleSharedDocsView(entity.Id, entity.Name, documents));
        }

        // all | entities → full view. When the grant hides amounts (NoAmounts, Spec 048), keep the
        // entity + documents but suppress the money-bearing fields — and don't even read the logs.
        IReadOnlyList<CurrencyTotal> yearTotals = [];
        IReadOnlyList<CareEntityPaymentLogSummary> recentLogs = [];

        if (!grant.NoAmounts)
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
                .Select(p => new CareEntityPaymentLogSummary(p.Id, p.Amount, p.Currency, p.Date, p.Channel))
                .ToList();
        }

        return new CircleSharedEntityResult(
            grant.Scope,
            Full: new CircleSharedEntityView(MapEntity(entity), yearTotals, recentLogs, documents),
            DocsOnly: null);
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
        => new(
            e.Id, e.Kind, e.AssetType, e.Name, e.CountryCode, e.Relationship, e.Emoji, e.PhotoDocumentId,
            ParseAttributes(e.AttributesJson), e.Archived, e.CreatedAt, e.UpdatedAt);

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
