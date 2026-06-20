using Aonik.Finance.Contracts.Models.Payments;
using Aonik.Finance.Contracts.Services.Payments;
using Aonik.Finance.Entities.Payments;
using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions;
using System.Text.RegularExpressions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Finance.Services.Payments;

/// <summary>
/// The customer card vault (Spec 007). Persists ONLY tokenised payment instruments — a gateway
/// vault token plus non-sensitive display metadata (brand, last four, expiry). No PAN, CVV, or
/// PCI-scoped data ever reaches Aonik storage. Every operation is scoped to the authenticated
/// customer's party (resolved server-side) and the current tenant, so one customer can never read
/// or mutate another's vault.
/// </summary>
internal sealed class PaymentMethodService : IPaymentMethodService
{
    private readonly FinanceDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IReadOnlyList<IPaymentProviderGateway> _gateways;

    public PaymentMethodService(
        FinanceDbContext dbContext,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider,
        IEnumerable<IPaymentProviderGateway> gateways)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
        _gateways = gateways.ToList();
    }

    public async Task<SetupIntentResponse> CreateSetupIntentAsync(CancellationToken cancellationToken = default)
    {
        var customerPartyId = await RequireCustomerPartyIdAsync(cancellationToken);
        var gateway = PrimaryGateway();

        // Reuse the customer's existing provider handle so repeat cards attach to one gateway customer.
        var existingCustomerRef = await _dbContext.PaymentMethods
            .AsNoTracking()
            .Where(m => m.CustomerPartyId == customerPartyId
                        && m.Provider == gateway.ProviderCode
                        && m.ProviderCustomerRef != null)
            .Select(m => m.ProviderCustomerRef)
            .FirstOrDefaultAsync(cancellationToken);

        var result = await gateway.CreateSetupIntentAsync(
            new PaymentProviderSetupIntentRequest(customerPartyId, existingCustomerRef),
            cancellationToken);

        return new SetupIntentResponse(
            result.Provider,
            result.ClientSecret,
            result.PaymentMethodTypes,
            result.SetupIntentReference,
            result.ProviderCustomerRef);
    }

    public async Task<IReadOnlyList<PaymentMethodResponse>> ListAsync(CancellationToken cancellationToken = default)
    {
        var customerPartyId = await ResolveCustomerPartyIdAsync(cancellationToken);
        if (customerPartyId is null)
        {
            return [];
        }

        var methods = await QueryOwned(customerPartyId.Value)
            .AsNoTracking()
            .OrderByDescending(m => m.IsDefault)
            .ThenByDescending(m => m.CreatedAt)
            .ToListAsync(cancellationToken);

        return methods.Select(Map).ToList();
    }

    public async Task<PaymentMethodResponse?> GetAsync(Guid paymentMethodId, CancellationToken cancellationToken = default)
    {
        var method = await GetOwnedAsync(paymentMethodId, cancellationToken);
        return method is null ? null : Map(method);
    }

    public async Task<PaymentMethodResponse> SaveAsync(
        SavePaymentMethodRequest request,
        CancellationToken cancellationToken = default)
    {
        var customerPartyId = await RequireCustomerPartyIdAsync(cancellationToken);

        var token = (request.ProviderToken ?? string.Empty).Trim();
        if (token.Length == 0)
        {
            throw new ArgumentException("A provider token is required.", nameof(request));
        }

        RejectRawPan(token, nameof(request.ProviderToken));
        var last4 = NormalizeLast4(request.Last4);
        var (expiryMonth, expiryYear) = NormalizeExpiry(request.ExpiryMonth, request.ExpiryYear);

        var provider = string.IsNullOrWhiteSpace(request.Provider)
            ? PrimaryGateway().ProviderCode
            : request.Provider.Trim();

        var type = string.IsNullOrWhiteSpace(request.Type) ? "card" : request.Type.Trim().ToLowerInvariant();
        var brand = Clean(request.Brand)?.ToLowerInvariant();
        var label = Clean(request.Label);
        var providerCustomerRef = Clean(request.ProviderCustomerRef);

        // Token-only vault: no PCI data may be smuggled through ANY persisted free-form field, not
        // just the token (a PAN in Provider/Label/Brand/Type/CustomerRef is rejected just the same).
        RejectRawPan(provider, nameof(request.Provider));
        RejectRawPan(type, nameof(request.Type));
        RejectRawPan(brand, nameof(request.Brand));
        RejectRawPan(label, nameof(request.Label));
        RejectRawPan(providerCustomerRef, nameof(request.ProviderCustomerRef));

        // Idempotent re-save: a token already vaulted for this customer updates its metadata in place.
        var method = await QueryOwned(customerPartyId)
            .FirstOrDefaultAsync(m => m.Provider == provider && m.ProviderToken == token, cancellationToken);

        var isNew = method is null;
        method ??= new PaymentMethod
        {
            TenantId = _tenantProvider.GetCurrentTenantId(),
            CustomerPartyId = customerPartyId,
            Provider = provider,
            ProviderToken = token,
        };

        method.Type = type;
        method.Brand = brand;
        method.Last4 = last4;
        method.ExpiryMonth = expiryMonth;
        method.ExpiryYear = expiryYear;
        method.Label = label;
        method.ProviderCustomerRef = providerCustomerRef ?? method.ProviderCustomerRef;

        if (isNew)
        {
            _dbContext.PaymentMethods.Add(method);
        }

        // The first saved card is the default; an explicit MakeDefault promotes this one and demotes the rest.
        var hasOtherDefault = await QueryOwned(customerPartyId)
            .AnyAsync(m => m.IsDefault && m.Id != method.Id, cancellationToken);

        if (request.MakeDefault || !hasOtherDefault)
        {
            await DemoteOtherDefaultsAsync(customerPartyId, method.Id, cancellationToken);
            method.IsDefault = true;
        }

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException) when (isNew)
        {
            // A concurrent request won the race on the unique (provider, token) or the single-default
            // index. Converge on the persisted state rather than surfacing a 500 — SaveAsync stays
            // idempotent on (provider, token), and the customer keeps exactly one default.
            _dbContext.ChangeTracker.Clear();

            var existing = await _dbContext.PaymentMethods
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    m => m.CustomerPartyId == customerPartyId && m.Provider == provider && m.ProviderToken == token,
                    cancellationToken);
            if (existing is not null)
            {
                return Map(existing); // same token already vaulted by the winner
            }

            // Otherwise a different card claimed the single default first — re-insert ours as non-default.
            method.IsDefault = false;
            _dbContext.PaymentMethods.Add(method);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return Map(method);
    }

    public async Task<bool> DeleteAsync(Guid paymentMethodId, CancellationToken cancellationToken = default)
    {
        var method = await GetOwnedAsync(paymentMethodId, cancellationToken);
        if (method is null)
        {
            return false;
        }

        var wasDefault = method.IsDefault;
        var customerPartyId = method.CustomerPartyId;

        _dbContext.PaymentMethods.Remove(method); // soft-delete via AonikDbContextBase
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Keep the customer with a default: promote the most-recently-added remaining card.
        if (wasDefault)
        {
            var next = await QueryOwned(customerPartyId)
                .OrderByDescending(m => m.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (next is not null)
            {
                next.IsDefault = true;
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        return true;
    }

    public async Task<IReadOnlyList<PaymentMethodResponse>> ListActiveAsync(CancellationToken cancellationToken = default)
    {
        var customerPartyId = await ResolveCustomerPartyIdAsync(cancellationToken);
        if (customerPartyId is null)
        {
            return [];
        }

        // "Active" aligns with gateway availability: a method is active only while the provider that
        // vaulted it is still a registered gateway. A method on a retired provider falls off this list.
        var availableProviders = _gateways
            .Select(g => g.ProviderCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var methods = await QueryOwned(customerPartyId.Value)
            .AsNoTracking()
            .OrderByDescending(m => m.IsDefault)
            .ThenByDescending(m => m.CreatedAt)
            .ToListAsync(cancellationToken);

        return methods
            .Where(m => availableProviders.Contains(m.Provider))
            .Select(Map)
            .ToList();
    }

    // ── helpers ──────────────────────────────────────────────────────────
    private IQueryable<PaymentMethod> QueryOwned(Guid customerPartyId)
        => _dbContext.PaymentMethods.Where(m => m.CustomerPartyId == customerPartyId);

    private async Task<PaymentMethod?> GetOwnedAsync(Guid id, CancellationToken cancellationToken)
    {
        var customerPartyId = await ResolveCustomerPartyIdAsync(cancellationToken);
        if (customerPartyId is null)
        {
            return null;
        }

        return await QueryOwned(customerPartyId.Value)
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
    }

    private async Task DemoteOtherDefaultsAsync(Guid customerPartyId, Guid keepId, CancellationToken cancellationToken)
    {
        var others = await QueryOwned(customerPartyId)
            .Where(m => m.IsDefault && m.Id != keepId)
            .ToListAsync(cancellationToken);

        foreach (var other in others)
        {
            other.IsDefault = false;
        }
    }

    private IPaymentProviderGateway PrimaryGateway()
        => _gateways.Count > 0
            ? _gateways[0]
            : throw new InvalidOperationException("No payment provider gateway is configured.");

    private Guid GetCurrentUserId()
        => _currentUserProvider.TryGetCurrentUserId(out var userId)
            ? userId
            : throw new InvalidOperationException("Authenticated user is required.");

    private async Task<Guid?> ResolveCustomerPartyIdAsync(CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var userId = GetCurrentUserId();

        return await _dbContext.UserParties
            .AsNoTracking()
            .Where(link => link.TenantId == tenantId && link.UserId == userId)
            .OrderByDescending(link => link.Id)
            .Select(link => (Guid?)link.PartyId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<Guid> RequireCustomerPartyIdAsync(CancellationToken cancellationToken)
        => await ResolveCustomerPartyIdAsync(cancellationToken)
            ?? throw new InvalidOperationException("No customer party is linked to the current user.");

    private static PaymentMethodResponse Map(PaymentMethod m)
        => new(m.Id, m.Provider, m.Type, m.Brand, m.Last4, m.ExpiryMonth, m.ExpiryYear, m.Label, m.IsDefault, m.CreatedAt);

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeLast4(string? last4)
    {
        var value = Clean(last4);
        if (value is null)
        {
            return null;
        }

        if (value.Length != 4 || !value.All(char.IsDigit))
        {
            throw new ArgumentException("Last4 must be exactly four digits.", nameof(last4));
        }

        return value;
    }

    private static (int? Month, int? Year) NormalizeExpiry(int? month, int? year)
    {
        if (month is { } m && m is < 1 or > 12)
        {
            throw new ArgumentException("Expiry month must be between 1 and 12.", nameof(month));
        }

        if (year is { } y && y is < 2000 or > 2100)
        {
            throw new ArgumentException("Expiry year is out of range.", nameof(year));
        }

        return (month, year);
    }

    // A run of 13–19 digits (optionally grouped by single spaces or hyphens) anywhere in a value —
    // standalone OR embedded in surrounding text — is almost certainly a raw PAN.
    private static readonly Regex PanLikePattern =
        new(@"[0-9](?:[ -]?[0-9]){12,18}", RegexOptions.Compiled);

    private static void RejectRawPan(string? value, string field)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        // Fail closed: never persist a field that contains a PAN-like run (Spec 007 token-only vault).
        if (PanLikePattern.IsMatch(value))
        {
            throw new ArgumentException(
                $"A raw card number must not be sent in '{field}'; provide tokenised data only.",
                field);
        }
    }
}
