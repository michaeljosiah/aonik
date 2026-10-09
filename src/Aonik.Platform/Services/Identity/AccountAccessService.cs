using System.Data;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text.Json;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Authentication;
using Aonik.Platform.Contracts.Services.Identity;
using Aonik.Platform.Entities.Identity;
using Aonik.Platform.Entities.Party;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Settings;
using Aonik.Platform.Settings;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Identity;
using Aonik.SharedKernel.Abstractions.Messaging;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Settings;
using Aonik.SharedKernel.Events.Integration;

namespace Aonik.Platform.Services.Identity;

internal sealed class AccountAccessService(
    PlatformDbContext db,
    ITenantProvider tenantProvider,
    IClock clock,
    IDataProtectionProvider protection,
    ITenantSettingStore settings,
    ITemplatedEmailSender emailSender,
    IUserProvisioningService provisioning,
    IIdpAccountServiceFactory accountServices,
    ISettingProvider settingProvider,
    IUserSessionBlocklist blocklist) : IAccountAccessService, IPaidAccountAccessService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        RespectRequiredConstructorParameters = true
    };

    public async Task IssueAsync(PaidAccountAccessRequest request, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantProvider.GetCurrentTenantId();
        var email = NormalizeEmail(request.Email);
        if (tenantId == Guid.Empty || tenantId != request.TenantId || email is null
            || request.CartId == Guid.Empty || request.OrderId == Guid.Empty
            || request.PaymentIntentId == Guid.Empty || request.GuestPartyId == Guid.Empty)
            throw new InvalidStateException("A paid guest checkout is required for account access.");

        var actionId = Guid.NewGuid();
        await InTransactionAsync(async () =>
        {
            var existing = await Actions().AsNoTracking().SingleOrDefaultAsync(
                x => x.Purpose == AccountAccessPurposes.PaidSetup && x.PaymentIntentId == request.PaymentIntentId,
                cancellationToken);
            if (existing is not null)
            {
                if (existing.CartId != request.CartId || existing.OrderId != request.OrderId
                    || existing.GuestPartyId != request.GuestPartyId || existing.Email != email)
                    throw new InvalidStateException("Account access does not match the recorded checkout.");
                return true;
            }

            var action = new AccountAccessAction
            {
                Id = actionId, TenantId = tenantId, Purpose = AccountAccessPurposes.PaidSetup,
                CartId = request.CartId, OrderId = request.OrderId, PaymentIntentId = request.PaymentIntentId,
                GuestPartyId = request.GuestPartyId, Email = email, Generation = Guid.NewGuid(),
                ExpiresAtUtc = clock.UtcNow.AddMinutes(10), SendWindowStartedAtUtc = clock.UtcNow
            };
            await QueueWithinBudgetAsync(action, cancellationToken);
            db.AccountAccessActions.Add(action);
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }, cancellationToken);
    }

    public async Task<bool> ResolveAsync(string? token, CancellationToken cancellationToken = default)
    {
        var action = await FindTokenAsync(token, cancellationToken);
        return action is not null && ((action.Status == "Pending" && action.ExpiresAtUtc > clock.UtcNow)
            || (action.Purpose == AccountAccessPurposes.EmailChange && action.Status == "Applying"));
    }

    public async Task ResendAsync(string? token, CancellationToken cancellationToken = default)
    {
        await InTransactionAsync(async () =>
        {
            var action = await FindTokenAsync(token, cancellationToken);
            if (action is null || action.Status != "Pending") return false;
            if (!await QueueWithinBudgetAsync(action, cancellationToken, rotate: true)) return false;
            AttachAction(action);
            db.Entry(action).State = EntityState.Modified;
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }, cancellationToken, conflictIsFalse: true);
    }

    public async Task RequestEmailChangeAsync(string? newEmail, AccountAccessIdentityProof proof,
        CancellationToken cancellationToken = default)
    {
        var email = NormalizeEmail(newEmail);
        if (email is null || !FreshProof(proof) || proof.ExistingUserId is null
            || !proof.Subject.StartsWith("auth0|", StringComparison.Ordinal)) return;
        var provider = await settingProvider.GetAsync(AuthSettingNames.Provider, cancellationToken);
        if (!string.Equals(provider, "Auth0", StringComparison.OrdinalIgnoreCase)) return;
        await InTransactionAsync(async () =>
        {
            var user = await FindUserAsync(proof, cancellationToken);
            if (user is null || email == NormalizeEmail(user.Email)
                || await EmailInUseAsync(email, user.Id, cancellationToken)) return false;
            // Applying means the provider may already have changed. Never supersede that recovery record.
            if (await Actions().AnyAsync(x => x.UserId == user.Id && x.Purpose == AccountAccessPurposes.EmailChange
                && x.Status == "Applying", cancellationToken)) return false;
            var window = clock.UtcNow.AddMinutes(-15);
            var userSends = await Actions().Where(x => x.UserId == user.Id && x.SendWindowStartedAtUtc > window)
                .SumAsync(x => x.SendCount, cancellationToken);
            if (userSends >= 5) return false;
            var action = new AccountAccessAction
            {
                Id = Guid.NewGuid(), TenantId = user.TenantId, Purpose = AccountAccessPurposes.EmailChange,
                UserId = user.Id, ExternalIssuer = user.ExternalIssuer, ExternalSubject = user.ExternalSubject,
                IdentityRevision = user.IdentityRevision, OriginalEmail = user.Email, Email = email,
                Generation = Guid.NewGuid(), ExpiresAtUtc = clock.UtcNow.AddMinutes(10),
                SendWindowStartedAtUtc = clock.UtcNow
            };
            if (!await QueueWithinBudgetAsync(action, cancellationToken)) return false;
            var pending = await Actions().AsNoTracking().Where(x => x.UserId == user.Id
                && x.Purpose == AccountAccessPurposes.EmailChange && x.Status == "Pending").ToListAsync(cancellationToken);
            foreach (var previous in pending) { AttachAction(previous); previous.Status = "Revoked"; }
            // Claim the parent user as well as the action; two requests cannot create independent active changes.
            user.UpdatedAt = clock.UtcNow;
            db.Entry(user).Property(x => x.UpdatedAt).IsModified = true;
            db.AccountAccessActions.Add(action);
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }, cancellationToken, conflictIsFalse: true);
    }

    public async Task<bool> CompleteAsync(string? token, AccountAccessIdentityProof proof, string purpose,
        CancellationToken cancellationToken = default)
    {
        if (!ValidProof(proof)) return false;
        if (purpose == AccountAccessPurposes.EmailChange)
            return await CompleteEmailChangeAsync(token, proof, cancellationToken);
        if (purpose != AccountAccessPurposes.PaidSetup) return false;
        return await InTransactionAsync(async () =>
        {
            var action = await FindTokenAsync(token, cancellationToken);
            if (action is null || action.Purpose != purpose || action.Status != "Pending"
                || action.ExpiresAtUtc <= clock.UtcNow || action.Email != NormalizeEmail(proof.VerifiedEmail)) return false;
            var user = await FindUserAsync(proof, cancellationToken);
            if (user is null)
            {
                if (proof.ExistingUserId.HasValue
                    || await db.Users.AnyAsync(x => x.TenantId == proof.TenantId && x.ExternalIssuer == proof.Issuer
                        && x.ExternalSubject == proof.Subject, cancellationToken)
                    || await EmailInUseAsync(action.Email, null, cancellationToken)) return false;
                user = new User
                {
                    Id = Guid.NewGuid(), TenantId = action.TenantId, ExternalIssuer = proof.Issuer,
                    ExternalSubject = proof.Subject, Email = action.Email, Status = "Active"
                };
                db.Users.Add(user);
            }
            else if (NormalizeEmail(user.Email) != action.Email) return false;
            user.UpdatedAt = clock.UtcNow;
            if (db.Entry(user).State != EntityState.Added) db.Entry(user).Property(x => x.UpdatedAt).IsModified = true;
            AttachAction(action);
            action.Status = "Consumed";
            action.ConsumedAtUtc = clock.UtcNow;
            action.ConsumedByUserId = user.Id;
            // The native user version serializes canonical-party creation across distinct paid actions.
            await db.SaveChangesAsync(cancellationToken);
            var result = await provisioning.EnsureUserAndCustomerAsync(
                new VerifiedIdentity(action.TenantId, proof.Issuer, proof.Subject, action.Email), cancellationToken);
            db.EnqueueIntegrationEvent(new AccountAccessVerifiedEvent(action.TenantId, action.Id,
                action.CartId!.Value, action.OrderId!.Value, action.PaymentIntentId!.Value,
                action.GuestPartyId!.Value, result.PartyId));
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }, cancellationToken, conflictIsFalse: true);
    }

    private async Task<bool> CompleteEmailChangeAsync(string? token, AccountAccessIdentityProof proof, CancellationToken ct)
    {
        if (!FreshProof(proof) || proof.ExistingUserId is null) return false;
        var provider = await settingProvider.GetAsync(AuthSettingNames.Provider, ct);
        if (!string.Equals(provider, "Auth0", StringComparison.OrdinalIgnoreCase)) return false;
        AccountAccessAction? applying = null;
        User? changingUser = null;
        var prepared = await InTransactionAsync(async () =>
        {
            var action = await FindTokenAsync(token, ct);
            var user = await FindUserAsync(proof, ct);
            if (action is null || user is null || !MatchesChange(action, user)
                || action.Status is not ("Pending" or "Applying")
                || (action.Status == "Pending" && action.ExpiresAtUtc <= clock.UtcNow)
                || await EmailInUseAsync(action.Email, user.Id, ct)) return false;
            AttachAction(action);
            action.Status = "Applying";
            await db.SaveChangesAsync(ct);
            applying = action;
            changingUser = user;
            return true;
        }, ct, conflictIsFalse: true);
        if (!prepared) return false;

        // Durable Applying precedes HTTP. An uncertain provider result is reconciled by the same action on retry.
        await accountServices.GetService(provider!).ConfirmVerifiedEmailAsync(
            changingUser!, applying!.OriginalEmail!, applying.Email, ct);
        var completed = await InTransactionAsync(async () =>
        {
            var action = await FindTokenAsync(token, ct);
            var user = await FindUserAsync(proof, ct);
            if (action is null || user is null || action.Status != "Applying" || !MatchesChange(action, user)
                || await EmailInUseAsync(action.Email, user.Id, ct)) return false;
            AttachAction(action);
            user.Email = action.Email;
            user.IdentityRevision++;
            user.UpdatedAt = clock.UtcNow;
            var partyIds = await db.UserParties.Where(x => x.TenantId == user.TenantId && x.UserId == user.Id
                && x.LinkType == "Individual").Select(x => x.PartyId).ToListAsync(ct);
            foreach (var entry in db.ChangeTracker.Entries<PartyContact>().Where(x => partyIds.Contains(x.Entity.PartyId)).ToList())
                entry.State = EntityState.Detached;
            foreach (var entry in db.ChangeTracker.Entries<Aonik.Platform.Entities.Party.Party>()
                .Where(x => x.Entity.TenantId == user.TenantId && partyIds.Contains(x.Entity.Id)).ToList())
                entry.State = EntityState.Detached;
            var party = await db.Parties.Include(x => x.Contacts)
                .SingleOrDefaultAsync(x => x.TenantId == user.TenantId && partyIds.Contains(x.Id), ct);
            if (party is not null)
            {
                var contact = party.Contacts.FirstOrDefault(x => x.Type == "Email" && x.IsPrimary);
                if (contact is null)
                    db.PartyContacts.Add(new PartyContact { PartyId = party.Id, Type = "Email", Value = action.Email, IsPrimary = true });
                else contact.Value = action.Email;
            }
            action.Status = "Consumed";
            action.ConsumedAtUtc = clock.UtcNow;
            action.ConsumedByUserId = user.Id;
            await blocklist.RevokeAsync(user.TenantId, user.Id, user.Id, "Confirmed email change", ct);
            await db.SaveChangesAsync(ct);
            return true;
        }, ct, conflictIsFalse: true);
        if (completed) await blocklist.InvalidateAsync(proof.TenantId, proof.ExistingUserId.Value, ct);
        return completed;
    }

    public async Task DeliverAsync(Guid actionId, Guid generation, CancellationToken cancellationToken = default)
    {
        var action = await Actions().AsNoTracking().SingleOrDefaultAsync(x => x.Id == actionId, cancellationToken);
        if (action is null || action.Generation != generation || action.Status != "Pending") return;
        var config = await ConfigurationAsync(cancellationToken)
            ?? throw new InvalidStateException("Account access delivery is not configured.");
        var ready = await InTransactionAsync(async () =>
        {
            action = await Actions().AsNoTracking().SingleOrDefaultAsync(x => x.Id == actionId, cancellationToken);
            if (action is null || action.Generation != generation || action.Status != "Pending") return false;
            if (action.DeliveryStartedAtUtc is null)
            {
                AttachAction(action);
                action.DeliveryStartedAtUtc = clock.UtcNow;
                action.ExpiresAtUtc = clock.UtcNow.AddMinutes(10);
                await db.SaveChangesAsync(cancellationToken);
            }
            else if (action.ExpiresAtUtc <= clock.UtcNow)
                throw new InvalidStateException("Account access delivery expired after sending began; request a new link.");
            return true;
        }, cancellationToken, conflictIsFalse: true);
        if (!ready) return;
        var path = action!.Purpose == AccountAccessPurposes.PaidSetup ? config.SetupPath : config.EmailChangePath;
        var token = $"{action.Id:N}.{action.Generation:N}.{Protector(action).Protect("confirm")}";
        await emailSender.SendAsync(new TemplatedEmailMessage(
            action.Purpose == AccountAccessPurposes.PaidSetup ? TransactionalEmailTemplateNames.AccountSetupAccess
                : TransactionalEmailTemplateNames.EmailChangeConfirmation,
            action.Email, new Dictionary<string, object?>
            {
                ["first_name"] = string.Empty,
                ["action_url"] = config.StorefrontOrigin.TrimEnd('/') + path + "#token=" + Uri.EscapeDataString(token),
                ["expires_at"] = action.ExpiresAtUtc.ToString("O")
            }), cancellationToken);
    }

    private async Task<bool> QueueWithinBudgetAsync(AccountAccessAction action, CancellationToken ct, bool rotate = false)
    {
        var since = clock.UtcNow.AddMinutes(-15);
        var targetSends = await Actions().Where(x => x.Email == action.Email && x.SendWindowStartedAtUtc > since)
            .SumAsync(x => x.SendCount, ct);
        var ownCount = action.SendWindowStartedAtUtc > since ? action.SendCount : 0;
        if (ownCount >= 5 || targetSends >= 10) return false;
        if (ownCount == 0) action.SendWindowStartedAtUtc = clock.UtcNow;
        action.SendCount = ownCount + 1;
        if (rotate)
        {
            action.Generation = Guid.NewGuid();
            action.DeliveryStartedAtUtc = null;
            action.ExpiresAtUtc = clock.UtcNow.AddMinutes(10);
        }
        db.EnqueueIntegrationEvent(new AccountAccessDeliveryRequestedEvent(action.TenantId, action.Id, action.Generation));
        return true;
    }

    private IQueryable<AccountAccessAction> Actions()
        => db.AccountAccessActions.Where(x => x.TenantId == tenantProvider.GetCurrentTenantId());

    private async Task<AccountAccessAction?> FindTokenAsync(string? token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 4096) return null;
        var parts = token.Split('.');
        if (parts.Length != 3 || !Guid.TryParseExact(parts[0], "N", out var id)
            || !Guid.TryParseExact(parts[1], "N", out var generation)) return null;
        var action = await Actions().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (action is null || action.Generation != generation) return null;
        try { return Protector(action).Unprotect(parts[2]) == "confirm" ? action : null; }
        catch (CryptographicException) { return null; }
    }

    private void AttachAction(AccountAccessAction action)
    {
        foreach (var entry in db.ChangeTracker.Entries<AccountAccessAction>()
            .Where(x => x.Entity.TenantId == action.TenantId && x.Entity.Id == action.Id).ToList())
            entry.State = EntityState.Detached;
        db.AccountAccessActions.Attach(action);
    }

    private IDataProtector Protector(AccountAccessAction action) => protection.CreateProtector("Aonik.AccountAccess.v1",
        action.TenantId.ToString("N"), action.Purpose, action.Id.ToString("N"), action.Generation.ToString("N"));

    private async Task<User?> FindUserAsync(AccountAccessIdentityProof proof, CancellationToken ct)
    {
        foreach (var entry in db.ChangeTracker.Entries<User>().Where(x => x.Entity.TenantId == proof.TenantId
            && x.Entity.ExternalIssuer == proof.Issuer && x.Entity.ExternalSubject == proof.Subject).ToList())
            entry.State = EntityState.Detached;
        var user = await db.Users.SingleOrDefaultAsync(x => x.TenantId == proof.TenantId
            && x.ExternalIssuer == proof.Issuer && x.ExternalSubject == proof.Subject, ct);
        if (user is null) return null;
        if (user.Status != "Active" || (proof.ExistingUserId.HasValue && proof.ExistingUserId != user.Id)
            || (proof.IdentityRevision.HasValue && proof.IdentityRevision != user.IdentityRevision)
            || await blocklist.IsRevokedAsync(user.TenantId, user.Id, proof.IssuedAtUtc, ct)) return null;
        return user;
    }

    private Task<bool> EmailInUseAsync(string email, Guid? exceptUserId, CancellationToken ct)
        => db.Users.AnyAsync(x => x.TenantId == tenantProvider.GetCurrentTenantId() && x.Id != exceptUserId
            && x.Email != null && x.Email.ToLower() == email, ct);

    private bool ValidProof(AccountAccessIdentityProof proof) => proof.TenantId != Guid.Empty
        && proof.TenantId == tenantProvider.GetCurrentTenantId() && !string.IsNullOrWhiteSpace(proof.Issuer)
        && !string.IsNullOrWhiteSpace(proof.Subject) && proof.Issuer.Length <= 500 && proof.Subject.Length <= 500
        && NormalizeEmail(proof.VerifiedEmail) is not null && proof.IssuedAtUtc <= clock.UtcNow.AddMinutes(1);

    private bool FreshProof(AccountAccessIdentityProof proof) => ValidProof(proof)
        && proof.AuthenticationTimeUtc is { } authenticated && authenticated >= clock.UtcNow.AddMinutes(-10)
        && authenticated <= clock.UtcNow.AddMinutes(1);

    private static bool MatchesChange(AccountAccessAction action, User user) => action.Purpose == AccountAccessPurposes.EmailChange
        && action.UserId == user.Id && action.ExternalIssuer == user.ExternalIssuer && action.ExternalSubject == user.ExternalSubject
        && action.IdentityRevision == user.IdentityRevision && NormalizeEmail(action.OriginalEmail) == NormalizeEmail(user.Email);

    private async Task<AccountAccessConfiguration?> ConfigurationAsync(CancellationToken ct)
    {
        var json = await settings.GetTenantValueAsync(AccountAccessSettingNames.Configuration, tenantProvider.GetCurrentTenantId(), ct);
        if (string.IsNullOrWhiteSpace(json) || json.Length > 6000) return null;
        try
        {
            var config = JsonSerializer.Deserialize<AccountAccessConfiguration>(json, JsonOptions);
            if (config is null || !config.IsEnabled || !Uri.TryCreate(config.StorefrontOrigin, UriKind.Absolute, out var origin)
                || origin.Scheme != "https" || origin.UserInfo.Length != 0 || origin.AbsolutePath != "/"
                || origin.Query.Length != 0 || origin.Fragment.Length != 0
                || !ValidPath(config.SetupPath) || !ValidPath(config.EmailChangePath)) return null;
            return config with { StorefrontOrigin = origin.GetLeftPart(UriPartial.Authority) };
        }
        catch (JsonException) { return null; }
    }

    private static bool ValidPath(string? path) => path is { Length: > 0 and <= 1000 } && path.StartsWith('/')
        && !path.StartsWith("//", StringComparison.Ordinal) && !path.Any(c => char.IsControl(c) || c is '?' or '#' or '\\');

    private static string? NormalizeEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > 320) return null;
        email = email.Trim();
        return MailAddress.TryCreate(email, out var parsed) && parsed.Address == email ? email.ToLowerInvariant() : null;
    }

    private async Task<bool> InTransactionAsync(Func<Task<bool>> operation, CancellationToken ct, bool conflictIsFalse = false)
    {
        // Retrying uses fresh instances; failed tracked writes must not leak into a later SaveChanges in this scope.
        var initial = db.ChangeTracker.Entries().Select(x => x.Entity).ToHashSet();
        try
        {
            return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var transaction = db.Database.IsRelational()
                    ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
                try
                {
                    var result = await operation();
                    if (transaction is not null) await transaction.CommitAsync(ct);
                    return result;
                }
                catch
                {
                    if (transaction is not null) await transaction.RollbackAsync(CancellationToken.None);
                    foreach (var entry in db.ChangeTracker.Entries().ToList())
                    {
                        if (!initial.Contains(entry.Entity)) entry.State = EntityState.Detached;
                    }
                    throw;
                }
            });
        }
        catch (DbUpdateException) when (conflictIsFalse) { return false; }
    }

    private sealed record VerifiedIdentity(Guid TenantId, string ExternalIssuer, string ExternalSubject, string? Email) : IExternalIdentity
    {
        public string? ExternalTenantId => null;
    }
}
