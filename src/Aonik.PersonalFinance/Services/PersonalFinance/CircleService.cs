using System.Security.Cryptography;
using System.Text.Json;
using Aonik.PersonalFinance.Contracts.Models;
using Aonik.PersonalFinance.Contracts.Services;
using Aonik.PersonalFinance.Entities;
using Aonik.PersonalFinance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Documents;
using Aonik.SharedKernel.Abstractions.Groups;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Platform;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Aonik.PersonalFinance.Services;

/// <summary>
/// PersonalFinance's circle API, now a projection over <see cref="IShareGrantService"/> (Spec 086 P5).
/// </summary>
/// <remarks>
/// The grant lifecycle, the single-use invite and the tenant scoping around them moved to
/// <c>Aonik.Groups</c>. What stays is the half that is genuinely about money and care: the
/// <c>CareEntity</c>, <c>PaymentLog</c> and document projections, the amount redaction a
/// <c>docsOnly</c> share means, and the response shapes the mobile app and CLI already consume.
/// Every route, DTO and status code is unchanged.
/// </remarks>
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
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IDocumentLinkReader _documentLinkReader;
    private readonly IPartyReader _partyReader;
    private readonly MemberPartyResolver _partyResolver;
    private readonly IShareGrantService _shareGrants;
    private readonly CircleInviteOptions _options;

    public CircleService(
        PersonalFinanceDbContext dbContext,
        ITenantProvider tenantProvider,
        ITenantContext tenantContext,
        ICurrentUserProvider currentUserProvider,
        IDocumentLinkReader documentLinkReader,
        IPartyReader partyReader,
        MemberPartyResolver partyResolver,
        IShareGrantService shareGrants,
        IOptions<CircleInviteOptions> options)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _tenantContext = tenantContext;
        _currentUserProvider = currentUserProvider;
        _documentLinkReader = documentLinkReader;
        _partyReader = partyReader;
        _partyResolver = partyResolver;
        _shareGrants = shareGrants;
        _options = options.Value;
    }

    // ── Grants ──────────────────────────────────────────────────────────

    public async Task<CircleGrantResponse> CreateGrantAsync(CreateCircleGrantRequest request, CancellationToken cancellationToken = default)
    {
        var (tenantId, _) = GetContext();
        var scope = NormalizeScope(request.Scope);
        var noAmounts = scope == "docsOnly" || request.NoAmounts;

        var memberPartyId = request.MemberUserId == Guid.Empty
            ? null
            : await _partyResolver.ResolveAsync(tenantId, request.MemberUserId, cancellationToken);

        var grant = await _shareGrants.CreateGrantAsync(
            new CreateShareGrantCommand(
                scope,
                ShareResourceKinds.CareEntity,
                request.EntityIds ?? [],
                memberPartyId,
                request.MemberUserId == Guid.Empty ? null : request.MemberUserId,
                TermsJson: CircleGrantTerms.Serialize(noAmounts)),
            cancellationToken);

        await MirrorLegacyNoAmountsAsync(grant.Id, inviteId: null, noAmounts, cancellationToken);

        return MapGrant(grant, noAmounts);
    }

    public async Task<IReadOnlyList<CircleGrantResponse>> ListGrantsForOwnerAsync(CancellationToken cancellationToken = default)
    {
        var grants = await _shareGrants.ListMineAsync(cancellationToken);
        return grants.Where(IsCareEntityGrant).Select(MapGrant).ToList();
    }

    public async Task<IReadOnlyList<CircleGrantResponse>> ListGrantsForMemberAsync(CancellationToken cancellationToken = default)
    {
        var grants = await _shareGrants.ListSharedWithMeAsync(cancellationToken);
        return grants.Where(IsCareEntityGrant).Select(MapGrant).ToList();
    }

    public Task<bool> RevokeGrantAsync(Guid grantId, CancellationToken cancellationToken = default)
        => _shareGrants.RevokeAsync(grantId, cancellationToken);

    public Task<bool> RevokeInviteAsync(Guid inviteId, CancellationToken cancellationToken = default)
        => _shareGrants.RevokeInviteAsync(inviteId, cancellationToken);

    // ── Invites ─────────────────────────────────────────────────────────

    public async Task<CircleInviteResponse> CreateInviteAsync(CreateCircleInviteRequest request, CancellationToken cancellationToken = default)
    {
        var scope = NormalizeScope(request.Scope);
        var noAmounts = scope == "docsOnly" || request.NoAmounts;

        var invite = await _shareGrants.CreateInviteAsync(
            new CreateShareInviteCommand(
                scope,
                ShareResourceKinds.CareEntity,
                request.EntityIds ?? [],
                TermsJson: CircleGrantTerms.Serialize(noAmounts),
                Channel: Clean(request.Channel),
                ValidFor: TimeSpan.FromDays(InviteExpiryDays)),
            cancellationToken);

        await MirrorLegacyNoAmountsAsync(grantId: null, invite.Id, noAmounts, cancellationToken);

        return MapInvite(invite, noAmounts);
    }

    /// <summary>
    /// Writes the redaction flag to the retained <c>NoAmounts</c> column as well as to terms.
    /// </summary>
    /// <remarks>
    /// The platform cannot do this: <c>NoAmounts</c> is finance vocabulary, and a platform that
    /// branched on a term would be exactly the coupling ADR-015 removes. But §10.2 keeps the column
    /// through the transition so a rollback needs no data recovery — and leaving it unwritten would
    /// mean a rollback to a pre-P5 build read <c>false</c> on every new grant and served amounts to
    /// docs-only members. So the owning module mirrors it, in a second save. If that save fails the
    /// current build is still correct, because it reads terms; only the rollback path degrades, and
    /// only to where it already was. The mirror goes when the column does.
    /// </remarks>
    private async Task MirrorLegacyNoAmountsAsync(Guid? grantId, Guid? inviteId, bool noAmounts, CancellationToken cancellationToken)
    {
        if (!noAmounts)
        {
            return;
        }

        if (grantId is { } id)
        {
            var grant = await _dbContext.CircleGrants.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (grant is not null)
            {
                grant.NoAmounts = true;
            }
        }

        if (inviteId is { } invite)
        {
            var row = await _dbContext.CircleInvites.FirstOrDefaultAsync(item => item.Id == invite, cancellationToken);
            if (row is not null)
            {
                row.NoAmounts = true;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<InvitePreviewResponse?> PreviewInviteAsync(string token, CancellationToken cancellationToken = default)
    {
        var preview = await _shareGrants.PreviewInviteAsync(token, cancellationToken);

        if (preview is null)
        {
            return null;
        }

        // The disclosure dial is a PersonalFinance decision, not a platform one, so the platform
        // preview always resolves names and this decides whether to show them. Putting the dial in
        // the platform would make one product's privacy policy everyone's.
        var disclose = _options.PreviewDisclosure == InvitePreviewDisclosure.Names;
        var names = preview.Resources.Select(resource => resource.DisplayName).ToList();

        return new InvitePreviewResponse(
            OwnerDisplayName: preview.OwnerDisplayName,
            Scope: preview.Scope,
            ScopeLabel: ScopeLabel(preview.Scope),
            EntityNames: disclose ? names : Array.Empty<string>(),
            EntityCount: preview.ResourceCount,
            NoAmounts: ReadNoAmounts(preview.TermsJson, preview.Scope),
            ExpiresAt: preview.ExpiresAt);
    }

    public async Task<AcceptInviteResult> AcceptInviteAsync(string token, CancellationToken cancellationToken = default)
    {
        var result = await _shareGrants.AcceptInviteAsync(token, cancellationToken);

        return result.Status switch
        {
            ShareInviteAcceptStatus.Accepted => AcceptInviteResult.FromGrant(MapGrant(result.Grant!)),
            ShareInviteAcceptStatus.SelfAccept => AcceptInviteResult.SelfAccept,
            _ => AcceptInviteResult.Invalid
        };
    }

    private static string ScopeLabel(string scope) => scope switch
    {
        "all" => "Everything they look after",
        "entities" => "Selected people & places",
        "docsOnly" => "Documents only",
        _ => "Selected people & places",
    };

    // ── Visibility filter ───────────────────────────────────────────────

    public async Task<CircleGrantView?> ResolveAsync(Guid ownerUserId, CancellationToken cancellationToken = default)
    {
        var grants = await GetActiveGrantsAsync(ownerUserId, cancellationToken);

        if (grants.Count == 0)
        {
            return null;
        }

        // Most permissive wins: "all" beats "entities", and amounts are allowed if ANY covering
        // grant allows them. Taking the least permissive would silently narrow a share the owner
        // deliberately widened.
        var scope = grants.Any(grant => grant.Scope == "all") ? "all" : grants[0].Scope;
        var entityIds = grants.SelectMany(grant => grant.EntityIds).Distinct().ToList();
        var noAmounts = grants.All(grant => grant.NoAmounts);

        return new CircleGrantView(ownerUserId, scope, entityIds, noAmounts);
    }

    /// <summary>
    /// Every active grant from one owner to the current member.
    /// </summary>
    /// <remarks>
    /// Reads the table directly rather than through <c>IShareGrantReader</c>, and that is deliberate
    /// for the length of the transition. The reader is party-keyed; this filter has to answer for
    /// grants written <em>before</em> the P3 backfill, which carry a null party — and a party-only
    /// read would make them vanish, which for a visibility filter means silently revoking access
    /// nobody revoked. So it matches owner and member on party <b>or</b> user, over a table
    /// PersonalFinance still owns <c>DbSet</c>s for (§10.3). It moves behind the reader when the
    /// user columns are dropped and there is nothing left to fall back to.
    /// </remarks>
    private async Task<IReadOnlyList<CircleGrantView>> GetActiveGrantsAsync(Guid ownerUserId, CancellationToken cancellationToken)
    {
        var (tenantId, memberUserId) = GetContext();
        var memberPartyId = await _partyResolver.ResolveAsync(tenantId, memberUserId, cancellationToken);
        var ownerPartyId = await _partyResolver.ResolveAsync(tenantId, ownerUserId, cancellationToken);

        var grants = await _dbContext.CircleGrants
            .AsNoTracking()
            .Where(grant => grant.TenantId == tenantId
                && grant.Status == "active"
                && (grant.ResourceKind == ShareResourceKinds.CareEntity || grant.ResourceKind == "")
                && (grant.OwnerUserId == ownerUserId || (ownerPartyId != null && grant.OwnerPartyId == ownerPartyId))
                && (grant.MemberUserId == memberUserId || (memberPartyId != null && grant.MemberPartyId == memberPartyId)))
            .OrderByDescending(grant => grant.CreatedAt)
            .ToListAsync(cancellationToken);

        return grants
            .Select(grant => new CircleGrantView(
                ownerUserId,
                grant.Scope,
                ParseIds(grant.EntityIdsJson),
                CircleGrantTerms.ReadNoAmounts(grant.TermsJson, grant.NoAmounts)))
            .ToList();
    }

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

    /// <summary>
    /// Only care-entity grants belong on the circle routes; another module's are not this one's to show.
    /// </summary>
    /// <remarks>
    /// An <b>empty</b> kind counts as care-entity. Every grant written before Spec 086 has one — the
    /// migration defaults the column to "" and the backfill that fills it is disabled by default — so
    /// filtering on the populated value alone would make every pre-existing share vanish the moment
    /// this deployed. Care entities are the only thing <c>EntityIdsJson</c> has ever held, so the
    /// empty value is not ambiguous. It stops being accepted when the backfill is confirmed
    /// everywhere and the column is made required.
    /// </remarks>
    private static bool IsCareEntityGrant(ShareGrantDto grant)
        => string.IsNullOrEmpty(grant.ResourceKind)
            || string.Equals(grant.ResourceKind, ShareResourceKinds.CareEntity, StringComparison.OrdinalIgnoreCase);

    private static bool ReadNoAmounts(string? termsJson, string scope)
        => CircleGrantTerms.ReadNoAmounts(termsJson, columnValue: scope == "docsOnly");

    private static CircleGrantResponse MapGrant(ShareGrantDto grant)
        => MapGrant(grant, ReadNoAmounts(grant.TermsJson, grant.Scope));

    private static CircleGrantResponse MapGrant(ShareGrantDto grant, bool noAmounts)
        => new(grant.Id, grant.OwnerUserId, grant.MemberUserId, grant.Scope, grant.ResourceIds, noAmounts, grant.Status, grant.CreatedAt);

    private static CircleInviteResponse MapInvite(ShareInviteDto invite, bool noAmounts)
        => new(invite.Id, invite.Token, invite.Scope, invite.ResourceIds, noAmounts, invite.Channel, invite.ExpiresAt, invite.Status);

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
