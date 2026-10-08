using System.Security.Cryptography;
using System.Text.Json;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

using Aonik.Platform.Contracts.Models.SignupLists;
using Aonik.Platform.Contracts.Services.SignupLists;
using Aonik.Platform.Entities.SignupLists;
using Aonik.Platform.Persistence;
using Aonik.Platform.Settings;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Settings;

namespace Aonik.Platform.Services.SignupLists;

internal sealed class SignupListService(
    PlatformDbContext dbContext,
    ITenantProvider tenantProvider,
    IClock clock,
    ITenantSettingStore settings,
    IDataProtectionProvider protection,
    ICurrentUserProvider currentUserProvider,
    IPermissionService permissionService)
    : AdminServiceBase(currentUserProvider, permissionService), ISignupListService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        RespectRequiredConstructorParameters = true
    };

    public async Task<SignupListsConfigurationDto> GetConfigurationAsync(CancellationToken cancellationToken = default)
    {
        var json = await settings.GetTenantValueAsync(SignupListSettingNames.Configuration,
            tenantProvider.GetCurrentTenantId(), cancellationToken);
        if (string.IsNullOrWhiteSpace(json) || json.Length > 4000) return new([]);
        try
        {
            var config = JsonSerializer.Deserialize<SignupListsConfigurationDto>(json, JsonOptions);
            if (config?.Lists is null || config.Lists.Count > 3
                || config.Lists.Any(list => !ValidDefinition(list))
                || config.Lists.Select(list => list.ListType).Distinct().Count() != config.Lists.Count)
                return new([]);
            return config;
        }
        catch (JsonException)
        {
            return new([]);
        }
    }

    public async Task CaptureAsync(string listType, SignupCaptureRequest request, CancellationToken cancellationToken = default)
    {
        RequireKnownList(listType);
        if (request is null) throw new InvalidStateException("A sign-up request is required.");
        var normalized = SignupCaptureValidator.Normalize(request);
        var validation = new SignupCaptureValidator(listType).Validate(normalized);
        if (!validation.IsValid)
            throw new InvalidStateException(string.Join(" ", validation.Errors.Select(error => error.ErrorMessage).Distinct()));

        var definition = (await GetConfigurationAsync(cancellationToken)).Lists
            .SingleOrDefault(list => list.ListType == listType);
        if (definition is null || definition.ConsentVersion != normalized.ConsentVersion)
            throw new InvalidStateException("This sign-up form is unavailable or has changed. Reload it before submitting.");
        if (listType == SignupListTypes.PrivateTable
            && !definition.Services!.Any(service => service.Id == normalized.Service))
            throw new InvalidStateException("Choose a service offered by this sign-up form.");

        var tenantId = tenantProvider.GetCurrentTenantId();
        var emailKey = normalized.Email.ToLowerInvariant();
        // An unverified repeat must not rewrite somebody else's details or reverse withdrawal.
        if (await Subscriptions(tenantId).AnyAsync(x => x.ListType == listType && x.NormalizedEmail == emailKey, cancellationToken))
            return;

        var subscription = new SignupSubscription
        {
            TenantId = tenantId,
            ListType = listType,
            Email = normalized.Email,
            NormalizedEmail = emailKey,
            Postcode = normalized.Postcode,
            PostcodeOutwardCode = normalized.Postcode?.Split(' ')[0],
            Name = normalized.Name,
            Phone = normalized.Phone,
            CountryCode = normalized.Country,
            Service = normalized.Service,
            ConsentVersion = definition.ConsentVersion,
            ConsentTextSnapshot = definition.ConsentText,
            ConsentSource = listType + "-form",
            ConsentedAtUtc = clock.UtcNow
        };
        dbContext.SignupSubscriptions.Add(subscription);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // As with notification deduplication, the SQL unique index settles insert races.
            dbContext.Entry(subscription).State = EntityState.Detached;
            if (!await Subscriptions(tenantId).AnyAsync(x => x.ListType == listType && x.NormalizedEmail == emailKey, cancellationToken))
                throw;
        }
    }

    public async Task<bool> UnsubscribeAsync(string listType, Guid subscriptionId, string? token,
        CancellationToken cancellationToken = default)
    {
        var tenantId = tenantProvider.GetCurrentTenantId();
        if (!SignupListTypes.IsKnown(listType) || !ValidToken(tenantId, listType, subscriptionId, token)) return false;
        var subscription = await Subscriptions(tenantId)
            .SingleOrDefaultAsync(x => x.Id == subscriptionId && x.ListType == listType, cancellationToken);
        if (subscription is null) return false;
        if (subscription.UnsubscribedAtUtc.HasValue) return true;
        subscription.UnsubscribedAtUtc = clock.UtcNow;
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.Entry(subscription).State = EntityState.Detached;
            if (!await Subscriptions(tenantId).AnyAsync(x => x.Id == subscriptionId && x.ListType == listType
                && x.UnsubscribedAtUtc != null, cancellationToken)) throw;
        }
        return true;
    }

    public async Task<PagedResult<SignupSubscriptionDto>> ListAsync(string listType, bool includeUnsubscribed = false,
        int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Customers.Read", cancellationToken);
        RequireKnownList(listType);
        if (pageNumber < 1 || pageSize is < 1 or > 100 || (long)(pageNumber - 1) * pageSize > int.MaxValue)
            throw new InvalidStateException("Page must be positive and page size between 1 and 100.");
        var tenantId = tenantProvider.GetCurrentTenantId();
        var query = Subscriptions(tenantId).AsNoTracking().Where(x => x.ListType == listType);
        if (!includeUnsubscribed) query = query.Where(x => x.UnsubscribedAtUtc == null);
        var count = await query.CountAsync(cancellationToken);
        var rows = await query.OrderBy(x => x.ConsentedAtUtc).ThenBy(x => x.Id)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new(rows.Select(row => new SignupSubscriptionDto(
            row.Id, row.ListType, row.Email, row.Postcode, row.PostcodeOutwardCode,
            row.Name, row.Phone, row.CountryCode, row.Service, row.ConsentVersion, row.ConsentTextSnapshot,
            row.ConsentSource, row.ConsentedAtUtc, row.UnsubscribedAtUtc,
            row.UnsubscribedAtUtc.HasValue ? null : Protector(tenantId, row.ListType, row.Id).Protect("unsubscribe")))
            .ToList(), count, pageNumber, pageSize);
    }

    public async Task<List<SignupAreaDemandDto>> GetAreaDemandAsync(CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Customers.Read", cancellationToken);
        return await Subscriptions(tenantProvider.GetCurrentTenantId()).AsNoTracking()
            .Where(x => x.ListType == SignupListTypes.DeliveryAvailability && x.UnsubscribedAtUtc == null)
            .GroupBy(x => x.PostcodeOutwardCode)
            .OrderBy(group => group.Key)
            .Select(group => new SignupAreaDemandDto(group.Key!, group.Count()))
            .ToListAsync(cancellationToken);
    }

    private IQueryable<SignupSubscription> Subscriptions(Guid tenantId)
        => dbContext.SignupSubscriptions.Where(x => x.TenantId == tenantId && !x.IsDeleted);

    private IDataProtector Protector(Guid tenantId, string listType, Guid subscriptionId)
        => protection.CreateProtector("Aonik.Platform.SignupListUnsubscribe.v1", tenantId.ToString("N"),
            listType, subscriptionId.ToString("N"));

    private bool ValidToken(Guid tenantId, string listType, Guid subscriptionId, string? token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 1024
            || token.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_'))
            return false;
        try
        {
            return Protector(tenantId, listType, subscriptionId).Unprotect(token) == "unsubscribe";
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException)
        {
            return false;
        }
    }

    private static bool ValidDefinition(SignupListDefinitionDto? definition)
        => definition is not null && SignupListTypes.IsKnown(definition.ListType)
            && Bounded(definition.ConsentVersion, 32) && Bounded(definition.ConsentText, 1000)
            && (definition.ListType == SignupListTypes.PrivateTable
                ? definition.Services is { Count: > 0 and <= 10 }
                    && definition.Services.All(service => service is not null && Bounded(service.Id, 64) && Bounded(service.Label, 100))
                    && definition.Services.Select(service => service.Id).Distinct().Count() == definition.Services.Count
                : definition.Services is null or { Count: 0 });

    private static bool Bounded(string? value, int maxLength)
        => !string.IsNullOrWhiteSpace(value) && value.Length <= maxLength && value == value.Trim();

    private static void RequireKnownList(string listType)
    {
        if (!SignupListTypes.IsKnown(listType)) throw new InvalidStateException("Unknown sign-up list.");
    }
}
