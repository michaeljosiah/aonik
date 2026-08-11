using System.Security.Cryptography;
using System.Text.Json;

using Aonik.Groups.Persistence;
using Aonik.PersonalFinance.Entities;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Groups;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Platform;
using Aonik.SharedKernel.Abstractions.UserBrief;
using Aonik.SharedKernel.Persistence;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Groups.Services;

/// <summary>
/// Scoped, revocable sharing of one party's records with another (Spec 086 §6, ADR-015).
/// </summary>
/// <remarks>
/// <para>
/// The generic half of what <c>CircleService</c> used to be: the grant lifecycle, the opaque
/// single-use invite, expiry, revocation, and the tenant scoping around all of it. What is shared
/// stays entirely opaque — a <c>ResourceKind</c> and a list of ids, resolved only by whichever module
/// registered an <see cref="IShareResourceResolver"/> for that kind. This service never learns what a
/// care entity is, and never reads <c>TermsJson</c>.
/// </para>
/// <para>
/// Grants are keyed by <b>party</b>. Through the Spec 086 transition they are read by party
/// <em>or</em> user, for the same reason <c>GroupService</c> does: the P3 backfill is disabled by
/// default, and a grant written before it ran has a null party. Reading on party alone would make
/// every such grant vanish — unlistable and, worse, unrevocable — which is precisely the failure the
/// spec's "party ids are new columns, not re-pointed ones" callout exists to prevent.
/// </para>
/// </remarks>
internal sealed class ShareGrantService : IShareGrantService, IShareGrantReader
{
    private const int InviteExpiryDays = 7;

    private static readonly HashSet<string> Scopes =
        new(StringComparer.OrdinalIgnoreCase) { ShareScopes.All, ShareScopes.Entities, ShareScopes.DocsOnly };

    private readonly IGroupDataContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUserPartyResolver _userPartyResolver;
    private readonly IPersonalFinancePartyResolver? _profilePartyFallback;
    private readonly IPartyReader _partyReader;
    private readonly IReadOnlyList<IShareResourceResolver> _resourceResolvers;
    private readonly IClock _clock;

    public ShareGrantService(
        IGroupDataContext dbContext,
        ITenantProvider tenantProvider,
        ITenantContext tenantContext,
        ICurrentUserProvider currentUserProvider,
        IUserPartyResolver userPartyResolver,
        IPartyReader partyReader,
        IEnumerable<IShareResourceResolver> resourceResolvers,
        IClock clock,
        IPersonalFinancePartyResolver? profilePartyFallback = null)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _tenantContext = tenantContext;
        _currentUserProvider = currentUserProvider;
        _userPartyResolver = userPartyResolver;
        _partyReader = partyReader;
        _resourceResolvers = resourceResolvers.ToList();
        _clock = clock;
        _profilePartyFallback = profilePartyFallback;

        // Two resolvers claiming one kind is a startup-shaped failure, not last-writer-wins:
        // ambiguity about who owns a resource is ambiguity about who may see it.
        var duplicates = _resourceResolvers
            .SelectMany(resolver => resolver.ResourceKinds)
            .GroupBy(kind => kind, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        if (duplicates.Count > 0)
        {
            throw new InvalidOperationException(
                $"More than one IShareResourceResolver claims the resource kind(s): {string.Join(", ", duplicates)}.");
        }
    }

    // ── Grants ──────────────────────────────────────────────────────────────────────────────

    public async Task<ShareGrantDto> CreateGrantAsync(CreateShareGrantCommand command, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var owner = await RequireCallerAsync(tenantId, cancellationToken);
        var scope = NormalizeScope(command.Scope);

        await ValidateResourcesAsync(command.ResourceKind, ToOwner(owner), command.ResourceIds, cancellationToken);

        // A grant naming party A and user B is authorised through BOTH: ListSharedWithMeAsync matches
        // the user while HasGrantAsync matches the party, so one malformed grant hands the owner's
        // resources to two different people. Fails closed when the pair cannot be verified.
        if (command.MemberPartyId is { } statedMemberParty && command.MemberUserId is { } statedMemberUser)
        {
            var resolved = await ResolvePartyForUserAsync(tenantId, statedMemberUser, cancellationToken);

            if (resolved is null || resolved != statedMemberParty)
            {
                throw new InvalidStateException("The party and user named on this grant are not the same person.");
            }
        }

        var grant = new CircleGrant
        {
            TenantId = tenantId,
            OwnerPartyId = owner.PartyId,
            OwnerUserId = owner.UserId,
            MemberPartyId = command.MemberPartyId,
            MemberUserId = command.MemberUserId
                ?? (command.MemberPartyId is { } memberPartyId
                    ? await ResolveUserForPartyAsync(tenantId, memberPartyId, cancellationToken)
                    : null),
            HouseholdId = command.GroupId,
            ResourceKind = command.ResourceKind,
            EntityIdsJson = SerializeIds(command.ResourceIds),
            TermsJson = command.TermsJson,
            Scope = scope,
            // Pending only when there is genuinely no member yet. Keyed on either identifier, because
            // a member known by user but not yet by party is still a member.
            Status = command.MemberPartyId is null && command.MemberUserId is null
                ? ShareGrantStatuses.Pending
                : ShareGrantStatuses.Active
        };

        _dbContext.ShareGrants.Add(grant);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(grant);
    }

    public async Task<IReadOnlyList<ShareGrantDto>> ListMineAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var caller = await RequireCallerAsync(tenantId, cancellationToken);

        var grants = await _dbContext.ShareGrants
            .AsNoTracking()
            .Where(grant => grant.TenantId == tenantId
                && grant.Status != ShareGrantStatuses.Revoked
                && ((caller.PartyId != null && grant.OwnerPartyId == caller.PartyId)
                    || grant.OwnerUserId == caller.UserId))
            .OrderByDescending(grant => grant.CreatedAt)
            .ToListAsync(cancellationToken);

        return grants.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<ShareGrantDto>> ListSharedWithMeAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var caller = await RequireCallerAsync(tenantId, cancellationToken);

        var grants = await _dbContext.ShareGrants
            .AsNoTracking()
            .Where(grant => grant.TenantId == tenantId
                && grant.Status == ShareGrantStatuses.Active
                && ((caller.PartyId != null && grant.MemberPartyId == caller.PartyId)
                    || grant.MemberUserId == caller.UserId))
            .OrderByDescending(grant => grant.CreatedAt)
            .ToListAsync(cancellationToken);

        return grants.Select(ToDto).ToList();
    }

    public async Task<bool> RevokeAsync(
        Guid grantId,
        string? requiredResourceKind = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var caller = await RequireCallerAsync(tenantId, cancellationToken);

        var grant = await _dbContext.ShareGrants
            .FirstOrDefaultAsync(item => item.Id == grantId
                && item.TenantId == tenantId
                && ((caller.PartyId != null && item.OwnerPartyId == caller.PartyId)
                    || item.OwnerUserId == caller.UserId), cancellationToken);

        // Not found and wrong-kind are the same answer, so an adapter cannot be used to discover
        // which of another product's grant ids exist.
        if (grant is null || !MatchesKind(grant.ResourceKind, requiredResourceKind))
        {
            return false;
        }

        grant.Status = ShareGrantStatuses.Revoked;

        // Revoking the grant also kills the token that minted it, so a replay of the consumed token
        // is unambiguously dead and the audit trail stays coherent — one invite, one grant. Same
        // save as the grant.
        var originatingInvite = await _dbContext.ShareInvites
            .FirstOrDefaultAsync(invite => invite.GrantId == grantId && invite.TenantId == tenantId, cancellationToken);

        if (originatingInvite is not null && originatingInvite.Status != ShareGrantStatuses.Revoked)
        {
            originatingInvite.Status = ShareGrantStatuses.Revoked;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Invites ─────────────────────────────────────────────────────────────────────────────

    public async Task<ShareInviteDto> CreateInviteAsync(CreateShareInviteCommand command, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var owner = await RequireCallerAsync(tenantId, cancellationToken);
        var scope = NormalizeScope(command.Scope);

        await ValidateResourcesAsync(command.ResourceKind, ToOwner(owner), command.ResourceIds, cancellationToken);

        var invite = new CircleInvite
        {
            TenantId = tenantId,
            OwnerPartyId = owner.PartyId,
            OwnerUserId = owner.UserId,
            Token = GenerateToken(),
            ResourceKind = command.ResourceKind,
            EntityIdsJson = SerializeIds(command.ResourceIds),
            TermsJson = command.TermsJson,
            Scope = scope,
            Channel = Clean(command.Channel),
            ExpiresAt = _clock.UtcNow.Add(command.ValidFor ?? TimeSpan.FromDays(InviteExpiryDays)),
            Status = ShareGrantStatuses.Pending
        };

        _dbContext.ShareInvites.Add(invite);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(invite);
    }

    public async Task<ShareInvitePreviewDto?> PreviewInviteAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        // The token IS the capability: 256 bits, globally unique. Resolved without an ambient tenant
        // because the caller is anonymous — the equality predicate pins the read to one invite.
        // !IsDeleted is explicit because AcrossTenants disables the soft-delete filter too. Without
        // it a withdrawn invite stays previewable to anyone still holding its token — the one thing
        // an anonymous, unauthenticated endpoint must never do.
        var invite = await _dbContext.ShareInvites
            .AsNoTracking()
            .AcrossTenants()
            .FirstOrDefaultAsync(item => item.Token == token && !item.IsDeleted, cancellationToken);

        // Fail closed, and identically: invalid, expired, consumed and revoked all return null, so
        // this cannot be used to discover which tokens exist.
        if (invite is null || invite.Status != ShareGrantStatuses.Pending || invite.ExpiresAt < _clock.UtcNow)
        {
            return null;
        }

        // Establish the tenant from the token so the cross-module owner-name read resolves. The
        // preview genuinely operates in this tenant once the token has validated.
        _tenantContext.TenantId = invite.TenantId;
        _tenantContext.ResolutionSource = "ShareInviteToken";

        var ownerName = await ResolveOwnerDisplayNameAsync(invite.TenantId, invite, cancellationToken);
        var resourceIds = ParseIds(invite.EntityIdsJson);

        // scope=all shares everything, so there is no specific list to name.
        var resources = string.Equals(invite.Scope, ShareScopes.All, StringComparison.OrdinalIgnoreCase)
            ? []
            : await ResolveResourcesAsync(
                // Normalised first. An invite minted before Spec 086 has an EMPTY kind, and
                // FindResolver would match nothing — so the preview would silently show a resource
                // count of zero for an invite that names several, which reads to the recipient as an
                // empty share rather than a lookup that never ran.
                NormalizeLegacyKind(invite.ResourceKind),
                new ShareResourceOwner(invite.OwnerPartyId, invite.OwnerUserId),
                resourceIds,
                cancellationToken);

        return new ShareInvitePreviewDto(ownerName, invite.ResourceKind, invite.Scope, invite.TermsJson, resources.Count, resources, invite.ExpiresAt);
    }

    public async Task<ShareInviteAcceptResult> AcceptInviteAsync(
        string token,
        string? requiredResourceKind = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var member = await RequireCallerAsync(tenantId, cancellationToken);

        var invite = await _dbContext.ShareInvites
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Token == token, cancellationToken);

        // Checked before anything is consumed, and indistinguishable from an unknown token so the
        // adapter cannot be used to probe which of another product's invites exist.
        if (invite is null || !MatchesKind(invite.ResourceKind, requiredResourceKind))
        {
            return ShareInviteAcceptResult.Invalid;
        }

        // Checked before status, so an owner tapping their own link always gets the same clear
        // conflict rather than a not-found that depends on timing.
        if (OwnedBy(invite, member))
        {
            return ShareInviteAcceptResult.SelfAccept;
        }

        // Idempotent resume: Spec 049's parked-token flow replays accept (cold start, then warm
        // link). If THIS member already consumed the token, hand back the grant they hold. A
        // different member reaching a consumed token gets Invalid — single-use, fail closed.
        if (string.Equals(invite.Status, "accepted", StringComparison.OrdinalIgnoreCase))
        {
            return await ResolveIdempotentAsync(tenantId, member, invite.GrantId, cancellationToken);
        }

        if (invite.Status != ShareGrantStatuses.Pending)
        {
            return ShareInviteAcceptResult.Invalid;
        }

        if (invite.ExpiresAt < _clock.UtcNow)
        {
            invite.Status = "expired";
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ShareInviteAcceptResult.Invalid;
        }

        var grant = new CircleGrant
        {
            TenantId = tenantId,
            // Carried from the invite, not re-resolved: the invite is the record of what was
            // offered, and re-resolving could pick up a link minted since it was sent.
            OwnerPartyId = invite.OwnerPartyId,
            OwnerUserId = invite.OwnerUserId,
            MemberPartyId = member.PartyId,
            MemberUserId = member.UserId,
            ResourceKind = invite.ResourceKind,
            EntityIdsJson = invite.EntityIdsJson,
            TermsJson = invite.TermsJson,
            Scope = invite.Scope,
            NoAmounts = invite.NoAmounts,
            Status = ShareGrantStatuses.Active
        };

        _dbContext.ShareGrants.Add(grant);

        // One save, so the grant and the token's consumption commit together: a crash between two
        // saves would leave the grant created and the token still reusable. The invite's RowVersion
        // makes two overlapping accepts conflict, and the loser resolves idempotently against the
        // winner rather than minting a second grant.
        invite.Status = "accepted";
        invite.ConsumedAt = _clock.UtcNow;
        invite.GrantId = grant.Id;

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ShareInviteAcceptResult.FromGrant(ToDto(grant));
        }
        catch (DbUpdateConcurrencyException)
        {
            _dbContext.ChangeTracker.Clear();

            var winner = await _dbContext.ShareInvites
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Token == token, cancellationToken);

            return await ResolveIdempotentAsync(tenantId, member, winner?.GrantId, cancellationToken);
        }
    }

    public async Task<bool> RevokeInviteAsync(
        Guid inviteId,
        string? requiredResourceKind = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var caller = await RequireCallerAsync(tenantId, cancellationToken);

        var invite = await _dbContext.ShareInvites
            .FirstOrDefaultAsync(item => item.Id == inviteId
                && item.TenantId == tenantId
                && ((caller.PartyId != null && item.OwnerPartyId == caller.PartyId)
                    || item.OwnerUserId == caller.UserId), cancellationToken);

        // Not found, not owned, wrong tenant and wrong kind are one answer, so existence is never
        // revealed.
        if (invite is null || !MatchesKind(invite.ResourceKind, requiredResourceKind))
        {
            return false;
        }

        // Idempotent: a DELETE can be retried.
        if (invite.Status == ShareGrantStatuses.Revoked)
        {
            return true;
        }

        if (invite.Status != ShareGrantStatuses.Pending)
        {
            throw new InvalidStateException(
                $"An invite that is already '{invite.Status}' cannot be revoked; revoke the grant instead.");
        }

        invite.Status = ShareGrantStatuses.Revoked;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Reader ──────────────────────────────────────────────────────────────────────────────

    public async Task<bool> HasGrantAsync(
        Guid memberPartyId,
        string resourceKind,
        Guid resourceId,
        CancellationToken cancellationToken = default)
    {
        var grants = await GetActiveGrantsAsync(memberPartyId, resourceKind, cancellationToken);

        return grants.Any(grant =>
            string.Equals(grant.Scope, ShareScopes.All, StringComparison.OrdinalIgnoreCase)
            || grant.ResourceIds.Contains(resourceId));
    }

    public async Task<IReadOnlyList<ShareGrantDto>> GetActiveGrantsAsync(
        Guid memberPartyId,
        string resourceKind,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var grants = await _dbContext.ShareGrants
            .AsNoTracking()
            .Where(grant => grant.TenantId == tenantId
                && grant.Status == ShareGrantStatuses.Active
                && grant.MemberPartyId == memberPartyId
                && grant.ResourceKind == resourceKind)
            .OrderByDescending(grant => grant.CreatedAt)
            .ToListAsync(cancellationToken);

        return grants.Select(ToDto).ToList();
    }

    // ── Resource validation ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Rejects an unknown resource kind, and any id the owner does not own.
    /// </summary>
    /// <remarks>
    /// Checking only that a resolver <em>exists</em> is not enough, and the gap is a privilege
    /// escalation rather than a tidiness problem: a caller could persist another party's ids into a
    /// grant and then read them back through <see cref="IShareGrantReader"/>, which answers from the
    /// stored ids alone. So the resolver is called <b>scoped to the owner</b>, and the returned set
    /// must match the requested one exactly. Equality, not containment — a resolver returning more
    /// than was asked for is a resolver bug, and accepting it silently would defeat the check.
    /// </remarks>
    private async Task ValidateResourcesAsync(
        string resourceKind,
        ShareResourceOwner owner,
        IReadOnlyList<Guid> resourceIds,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(resourceKind))
        {
            throw new InvalidStateException("A share grant must name a resource kind.");
        }

        if (FindResolver(resourceKind) is null)
        {
            // Fails closed the way the agent approval gate does for an unclassified tool: an open
            // string with no registered owner is a typo sink, not an extension point.
            throw new InvalidStateException($"No resolver is registered for resource kind '{resourceKind}'.");
        }

        if (resourceIds.Count == 0)
        {
            return;
        }

        var resolved = await ResolveResourcesAsync(resourceKind, owner, resourceIds, cancellationToken);

        var requested = resourceIds.Distinct().ToHashSet();
        var returned = resolved.Select(resource => resource.Id).ToHashSet();

        // The SET, not the count. Counting alone lets a faulty or newly contributed resolver drop an
        // unauthorised id and return a different owned one in its place — the totals agree, and the
        // caller's original unauthorised id is persisted, which IShareGrantReader then authorises
        // from directly. Equality is what the paragraph above actually promises.
        if (!requested.SetEquals(returned))
        {
            throw new InvalidStateException("A share grant can only name resources its owner owns.");
        }
    }

    private async Task<IReadOnlyList<ShareResourceRef>> ResolveResourcesAsync(
        string resourceKind,
        ShareResourceOwner owner,
        IReadOnlyList<Guid> resourceIds,
        CancellationToken cancellationToken)
    {
        if ((owner.PartyId is null && owner.UserId is null)
            || resourceIds.Count == 0
            || FindResolver(resourceKind) is not { } resolver)
        {
            return [];
        }

        return await resolver.ResolveAsync(resourceKind, owner, resourceIds.Distinct().ToList(), cancellationToken);
    }

    private IShareResourceResolver? FindResolver(string resourceKind)
        => _resourceResolvers.FirstOrDefault(resolver =>
            resolver.ResourceKinds.Contains(resourceKind, StringComparer.OrdinalIgnoreCase));

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The caller. The <b>user</b> is required — they are authenticated — and the <b>party</b> is
    /// best-effort.
    /// </summary>
    /// <remarks>
    /// Party-optional, deliberately. Sharing worked before Spec 086 for anyone with a login, party
    /// or no party, and making a party link mandatory at the cutover would silently take the feature
    /// away from every user who has not got one. Reads, accepts and revocations therefore fall back
    /// to the user key, exactly as <c>GroupService</c> does. The single exception is
    /// <see cref="RequireOwnerParty"/> — see there.
    /// </remarks>
    private readonly record struct Caller(Guid? PartyId, Guid UserId);

    private async Task<Caller> RequireCallerAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        if (!_currentUserProvider.TryGetCurrentUserId(out var userId))
        {
            throw new PermissionDeniedException("An authenticated user is required.");
        }

        var partyId = await _userPartyResolver.GetPartyIdForUserAsync(tenantId, userId, cancellationToken);

        if (partyId is null && _profilePartyFallback is not null)
        {
            partyId = await _profilePartyFallback.GetPartyIdForUserAsync(tenantId, userId, cancellationToken);
        }

        return new Caller(partyId, userId);
    }

    private static ShareResourceOwner ToOwner(Caller caller) => new(caller.PartyId, caller.UserId);

    /// <summary>Party to user, through the bridge then the profile — the mirror of the caller lookup.</summary>
    /// <summary>User to party, through the bridge then the profile.</summary>
    private async Task<Guid?> ResolvePartyForUserAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        var partyId = await _userPartyResolver.GetPartyIdForUserAsync(tenantId, userId, cancellationToken);

        if (partyId is null && _profilePartyFallback is not null)
        {
            partyId = await _profilePartyFallback.GetPartyIdForUserAsync(tenantId, userId, cancellationToken);
        }

        return partyId;
    }

    private async Task<Guid?> ResolveUserForPartyAsync(Guid tenantId, Guid partyId, CancellationToken cancellationToken)
    {
        var userId = await _userPartyResolver.GetUserIdForPartyAsync(tenantId, partyId, cancellationToken);

        if (userId is null && _profilePartyFallback is not null)
        {
            userId = await _profilePartyFallback.GetUserIdForPartyAsync(tenantId, partyId, cancellationToken);
        }

        return userId;
    }

    /// <summary>
    /// Whether a stored kind satisfies the adapter's required one.
    /// </summary>
    /// <remarks>
    /// An <b>empty</b> stored kind matches anything, because every row written before Spec 086 has
    /// one and the backfill that fills it is disabled by default — refusing those would make an
    /// upgrade unable to revoke its own pre-existing shares.
    /// </remarks>
    /// <summary>
    /// A stored kind, with the pre-Spec-086 empty value read as what it has always meant.
    /// </summary>
    private static string NormalizeLegacyKind(string? storedKind)
        => string.IsNullOrEmpty(storedKind) ? ShareResourceKinds.CareEntity : storedKind;

    private static bool MatchesKind(string? storedKind, string? requiredKind)
        => requiredKind is null
            || string.IsNullOrEmpty(storedKind)
            || string.Equals(storedKind, requiredKind, StringComparison.OrdinalIgnoreCase);

    private static bool OwnedBy(CircleInvite invite, Caller caller)
        => (invite.OwnerPartyId is { } ownerPartyId && ownerPartyId == caller.PartyId)
            || invite.OwnerUserId == caller.UserId;

    private async Task<ShareInviteAcceptResult> ResolveIdempotentAsync(
        Guid tenantId,
        Caller member,
        Guid? grantId,
        CancellationToken cancellationToken)
    {
        if (grantId is not { } id)
        {
            return ShareInviteAcceptResult.Invalid;
        }

        var grant = await _dbContext.ShareGrants
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id
                && item.TenantId == tenantId
                && item.Status == ShareGrantStatuses.Active
                && ((member.PartyId != null && item.MemberPartyId == member.PartyId)
                    || item.MemberUserId == member.UserId), cancellationToken);

        // A grant bound to someone else, or since revoked, confers nothing for this caller. Returning
        // a revoked grant as a success would falsely read as "you are in".
        return grant is null ? ShareInviteAcceptResult.Invalid : ShareInviteAcceptResult.FromGrant(ToDto(grant));
    }

    private async Task<string> ResolveOwnerDisplayNameAsync(Guid tenantId, CircleInvite invite, CancellationToken cancellationToken)
    {
        if (invite.OwnerPartyId is not { } ownerPartyId)
        {
            return "Someone";
        }

        var parties = await _partyReader.GetByIdsAsync(tenantId, [ownerPartyId], cancellationToken);
        var displayName = parties?.FirstOrDefault()?.DisplayName;

        return string.IsNullOrWhiteSpace(displayName) ? "Someone" : displayName.Trim();
    }

    private static string NormalizeScope(string? scope)
    {
        var candidate = string.IsNullOrWhiteSpace(scope) ? ShareScopes.Entities : scope.Trim();

        return Scopes.Contains(candidate)
            ? Scopes.First(known => string.Equals(known, candidate, StringComparison.OrdinalIgnoreCase))
            : throw new InvalidStateException($"Scope must be one of: {string.Join(", ", Scopes)}.");
    }

    private static string GenerateToken()
    {
        // 256 bits, url-safe. No signature or MAC — the row is the record of truth, which is what
        // makes revocation immediate and the token non-derivable.
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    private static string SerializeIds(IReadOnlyList<Guid>? ids)
        => JsonSerializer.Serialize(ids?.Distinct().ToList() ?? []);

    private static IReadOnlyList<Guid> ParseIds(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<Guid>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ShareGrantDto ToDto(CircleGrant grant) => new(
        grant.Id,
        grant.OwnerPartyId ?? Guid.Empty,
        grant.MemberPartyId,
        grant.OwnerUserId,
        grant.MemberUserId,
        grant.HouseholdId,
        grant.Scope,
        grant.ResourceKind,
        ParseIds(grant.EntityIdsJson),
        grant.TermsJson,
        grant.Status,
        grant.CreatedAt);

    private static ShareInviteDto ToDto(CircleInvite invite) => new(
        invite.Id,
        invite.Token,
        invite.Scope,
        invite.ResourceKind,
        ParseIds(invite.EntityIdsJson),
        invite.TermsJson,
        invite.Channel,
        invite.ExpiresAt,
        invite.Status,
        invite.ConsumedAt,
        invite.GrantId);
}
