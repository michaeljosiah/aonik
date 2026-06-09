using System.Security.Cryptography;
using System.Text;

using Aonik.Finance.Contracts.Services.Partners.Connectors;
using Microsoft.EntityFrameworkCore;

using Aonik.Finance.Contracts.Services.Payments;
using Aonik.Finance.Entities.Partners;
using Aonik.Finance.Entities.Payments;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.Partners.Connectors.Registry;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;

namespace Aonik.Finance.Services.Payments;

/// <summary>
/// Persists payout beneficiaries and the customer→recipient ownership graph. The party graph (party,
/// relationship edge, Beneficiary role) is written through the cross-module <see cref="IPartyService"/>
/// seam; the structured destination (<see cref="ExternalPayoutAccount"/>) is written through Finance's
/// own context. Each party-seam call persists independently (separate module DbContext), so this
/// service makes them idempotent rather than relying on a shared transaction.
/// </summary>
internal sealed class PayoutBeneficiaryService : IPayoutBeneficiaryService
{
    /// <summary>The recipient's Beneficiary role is scoped to the owning customer.</summary>
    private const string CustomerContextType = "Customer";

    private readonly FinanceDbContext _dbContext;
    private readonly IPartyService _partyService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IPartnerConnectorResolver _connectorResolver;
    private readonly Services.Partners.Connectors.IPartnerConnectorFactory _connectorFactory;
    private readonly IClock _clock;

    public PayoutBeneficiaryService(
        FinanceDbContext dbContext,
        IPartyService partyService,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider,
        IPartnerConnectorResolver connectorResolver,
        Services.Partners.Connectors.IPartnerConnectorFactory connectorFactory,
        IClock clock)
    {
        _dbContext = dbContext;
        _partyService = partyService;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
        _connectorResolver = connectorResolver;
        _connectorFactory = connectorFactory;
        _clock = clock;
    }

    public async Task<PayoutBeneficiaryResponse> SaveBeneficiaryAsync(
        SavePayoutBeneficiaryRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.CustomerPartyId == Guid.Empty)
        {
            throw new ArgumentException("Customer party id is required.", nameof(request.CustomerPartyId));
        }

        if (string.IsNullOrWhiteSpace(request.DestinationType))
        {
            throw new ArgumentException("Destination type is required.", nameof(request.DestinationType));
        }

        if (string.IsNullOrWhiteSpace(request.AccountName))
        {
            throw new ArgumentException("Account name is required.", nameof(request.AccountName));
        }

        if (string.IsNullOrWhiteSpace(request.Currency))
        {
            throw new ArgumentException("Currency is required.", nameof(request.Currency));
        }

        if (string.IsNullOrWhiteSpace(request.MaskedAccountIdentifier))
        {
            throw new ArgumentException("Masked account identifier is required.", nameof(request.MaskedAccountIdentifier));
        }

        var relationshipTypeCode = string.IsNullOrWhiteSpace(request.RelationshipTypeCode)
            ? PartyRelationshipTypeCodes.Recipient
            : request.RelationshipTypeCode.Trim();

        // 1) Resolve the recipient party — reuse an existing one or create it.
        var (beneficiaryPartyId, beneficiaryName) =
            await ResolveBeneficiaryPartyAsync(request, cancellationToken);

        // 2) Find-or-create the customer→recipient edge. CreateRelationshipAsync always inserts, so we
        //    dedupe against the customer's existing relationships first (no unique index backs this).
        await EnsureRelationshipAsync(
            request.CustomerPartyId,
            beneficiaryPartyId,
            relationshipTypeCode,
            request.Notes,
            cancellationToken);

        // 3) Mark the recipient payable in the context of this customer (idempotent).
        await _partyService.AssignPartyRoleAsync(
            beneficiaryPartyId,
            PartyRoleCodes.Beneficiary,
            CustomerContextType,
            request.CustomerPartyId,
            cancellationToken);

        // 4) Persist the structured payout destination (masked identifier + token only, never raw PAN/MSISDN).
        var account = new ExternalPayoutAccount
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantProvider.GetCurrentTenantId(),
            CustomerPartyId = request.CustomerPartyId,
            BeneficiaryPartyId = beneficiaryPartyId,
            PartnerId = request.PartnerId,
            ConnectorId = request.ConnectorId,
            DestinationType = request.DestinationType.Trim(),
            BankCode = Normalize(request.BankCode),
            BranchCode = Normalize(request.BranchCode),
            MobileNetwork = Normalize(request.MobileNetwork),
            MaskedAccountIdentifier = request.MaskedAccountIdentifier.Trim(),
            AccountName = request.AccountName.Trim(),
            Currency = request.Currency.Trim().ToUpperInvariant(),
            ProviderBeneficiaryId = Normalize(request.ProviderBeneficiaryId),
            IsVerified = false
        };

        _dbContext.ExternalPayoutAccounts.Add(account);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new PayoutBeneficiaryResponse(
            account.Id,
            request.CustomerPartyId,
            beneficiaryPartyId,
            beneficiaryName,
            account.DestinationType,
            account.MaskedAccountIdentifier,
            account.Currency,
            account.BankCode,
            account.MobileNetwork,
            account.AccountName,
            account.ProviderCode,
            relationshipTypeCode,
            account.IsVerified);
    }

    public async Task<PayoutBeneficiaryResponse> VerifyAndRegisterBeneficiaryAsync(
        VerifyPayoutBeneficiaryRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.CustomerPartyId == Guid.Empty)
        {
            throw new ArgumentException("Customer party id is required.", nameof(request.CustomerPartyId));
        }

        await EnsureCallerOwnsPartyAsync(request.CustomerPartyId, cancellationToken);

        var destinationType = NormalizeRail(request.DestinationType);
        var currency = NormalizeRequired(request.Currency, nameof(request.Currency)).ToUpperInvariant();
        var country = NormalizeRequired(request.Country, nameof(request.Country)).ToUpperInvariant();
        var fallbackName = NormalizeRequired(request.AccountName, nameof(request.AccountName));
        var rawDestination = BuildRawDestination(request, destinationType);
        var mask = MaskDestination(rawDestination);

        var connector = ResolveRegistrationConnector(request.ProviderCode, country, currency, destinationType);
        var providerCode = connector.ProviderCode;

        // Bind registration to a persisted Connector row (Spec 042 §9) so the beneficiary records the exact
        // account it was registered with; payouts to it then dispatch through that same connector and bundle.
        // Falls back to the unbound connector (Simulated / no row configured) when none is found.
        var connectorRow = await FindRegistrationConnectorRowAsync(providerCode, cancellationToken);
        if (connectorRow is not null)
        {
            connector = _connectorFactory.CreatePayout(connectorRow);
        }

        var fingerprint = BuildRailFingerprint(providerCode, country, currency, destinationType, rawDestination);

        var existing = await _dbContext.ExternalPayoutAccounts
            .FirstOrDefaultAsync(account => account.CustomerPartyId == request.CustomerPartyId
                                           && account.ProviderCode == providerCode
                                           && account.RailFingerprint == fingerprint,
                cancellationToken);

        if (existing is { IsVerified: true } && !string.IsNullOrWhiteSpace(existing.ProviderBeneficiaryId))
        {
            await EnsurePartyGraphAsync(request, existing.BeneficiaryPartyId, fallbackName, cancellationToken);
            var displayName = await ResolveBeneficiaryNameAsync(existing.BeneficiaryPartyId, fallbackName, cancellationToken);
            return ToResponse(existing, displayName, request.RelationshipTypeCode);
        }

        var resolvedName = await TryResolveAccountNameAsync(connector, request, destinationType, currency, cancellationToken);
        var accountName = resolvedName ?? fallbackName;

        var registration = await connector.RegisterRecipientAsync(
            new RecipientRegistrationRequest(
                rawDestination,
                currency,
                accountName,
                country,
                new Dictionary<string, string>
                {
                    ["customerPartyId"] = request.CustomerPartyId.ToString(),
                    ["destinationType"] = destinationType
                }),
            cancellationToken);

        if (!registration.Registered || string.IsNullOrWhiteSpace(registration.ProviderBeneficiaryId))
        {
            throw new InvalidStateException("Partner did not return a payout beneficiary id.");
        }

        var (beneficiaryPartyId, beneficiaryName) = await EnsurePartyGraphAsync(
            request, existing?.BeneficiaryPartyId, accountName, cancellationToken);

        var account = existing ?? new ExternalPayoutAccount
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantProvider.GetCurrentTenantId(),
            CustomerPartyId = request.CustomerPartyId
        };

        account.BeneficiaryPartyId = beneficiaryPartyId;
        account.DestinationType = destinationType;
        account.BankCode = Normalize(request.BankCode);
        account.BranchCode = Normalize(request.BranchCode);
        account.MobileNetwork = Normalize(request.MobileNetwork);
        account.MaskedAccountIdentifier = mask;
        account.RailFingerprint = fingerprint;
        account.AccountName = registration.AccountName ?? accountName;
        account.Currency = currency;
        account.ProviderCode = providerCode;
        account.ConnectorId = connectorRow?.Id;
        account.PartnerId = connectorRow?.PartnerId ?? account.PartnerId;
        account.ProviderBeneficiaryId = registration.ProviderBeneficiaryId.Trim();
        account.IsVerified = true;
        account.VerifiedAt = _clock.UtcNow;

        if (existing is null)
        {
            _dbContext.ExternalPayoutAccounts.Add(account);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToResponse(account, beneficiaryName, request.RelationshipTypeCode);
    }

    public async Task<IReadOnlyList<PayoutBeneficiaryResponse>> ListBeneficiariesAsync(
        Guid customerPartyId,
        CancellationToken cancellationToken = default)
    {
        if (customerPartyId == Guid.Empty)
        {
            throw new ArgumentException("Customer party id is required.", nameof(customerPartyId));
        }

        var relationships = await _partyService.GetRelationshipsAsync(customerPartyId, cancellationToken);

        // Outgoing edges from this customer identify the recipient parties they own. A party can have
        // more than one edge (e.g. Recipient + a kinship type); keep the first for display purposes.
        var recipientsByPartyId = relationships
            .Where(relationship => relationship.FromPartyId == customerPartyId)
            .GroupBy(relationship => relationship.ToPartyId)
            .ToDictionary(group => group.Key, group => group.First());

        if (recipientsByPartyId.Count == 0)
        {
            return Array.Empty<PayoutBeneficiaryResponse>();
        }

        var tenantId = _tenantProvider.GetCurrentTenantId();
        var recipientPartyIds = recipientsByPartyId.Keys.ToList();

        var accounts = await _dbContext.ExternalPayoutAccounts
            .AsNoTracking()
            .Where(account => account.TenantId == tenantId
                              && account.CustomerPartyId == customerPartyId
                              && account.BeneficiaryPartyId != null
                              && recipientPartyIds.Contains(account.BeneficiaryPartyId.Value))
            .OrderByDescending(account => account.CreatedAt)
            .ToListAsync(cancellationToken);

        return accounts
            .Select(account =>
            {
                var recipient = recipientsByPartyId[account.BeneficiaryPartyId!.Value];
                return new PayoutBeneficiaryResponse(
                    account.Id,
                    customerPartyId,
                    account.BeneficiaryPartyId.Value,
                    recipient.ToPartyName,
                    account.DestinationType,
                    account.MaskedAccountIdentifier,
                    account.Currency,
                    account.BankCode,
                    account.MobileNetwork,
                    account.AccountName,
                    account.ProviderCode,
                    recipient.RelationshipTypeCode,
                    account.IsVerified);
            })
            .ToList();
    }

    private async Task EnsureCallerOwnsPartyAsync(Guid customerPartyId, CancellationToken cancellationToken)
    {
        var userId = _currentUserProvider.GetCurrentUserId();
        if (userId is null || userId.Value == Guid.Empty)
        {
            throw new UnauthorizedAccessException("No authenticated user to authorize beneficiary verification.");
        }

        var owns = await _dbContext.UserParties
            .AsNoTracking()
            .AnyAsync(link => link.UserId == userId.Value && link.PartyId == customerPartyId, cancellationToken);

        if (!owns)
        {
            throw new UnauthorizedAccessException(
                "The requested customer party does not belong to the authenticated user.");
        }
    }

    /// <summary>
    /// The persisted payout connector row a beneficiary registration binds to (Spec 042 §9): prefers the
    /// migrated legacy-default connector for the resolved provider, else the first configured one, else null
    /// (no row — unbound Simulated / legacy registration). Explicit per-connector selection is available via
    /// <c>SaveBeneficiaryAsync</c>, which carries an explicit <c>ConnectorId</c>.
    /// </summary>
    private async Task<Connector?> FindRegistrationConnectorRowAsync(string providerCode, CancellationToken cancellationToken)
    {
        var payoutTypes = ConnectorRegistry.All
            .Where(k => k.Port == PartnerServiceCategory.Payout
                        && string.Equals(k.ProviderCode, providerCode, StringComparison.OrdinalIgnoreCase))
            .SelectMany(k => new[] { k.Kind, k.ProviderCode })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (payoutTypes.Count == 0)
        {
            return null;
        }

        var rows = await _dbContext.Connectors
            .Where(c => payoutTypes.Contains(c.ConnectorType))
            .ToListAsync(cancellationToken);

        return rows.FirstOrDefault(c => c.IsLegacyDefault) ?? rows.FirstOrDefault();
    }

    private IPartnerPayoutConnector ResolveRegistrationConnector(
        string? providerCode,
        string country,
        string currency,
        string destinationType)
    {
        var query = new PartnerConnectorQuery(PartnerServiceCategory.Payout, country, currency, destinationType);

        IPartnerPayoutConnector connector;
        if (!string.IsNullOrWhiteSpace(providerCode))
        {
            connector = _connectorResolver.ResolvePayoutConnector(providerCode.Trim());
        }
        else if (_connectorResolver.TryResolvePreferredPayoutConnector(query, out var preferred) && preferred is not null)
        {
            connector = preferred;
        }
        else if (_connectorResolver.TryResolvePayoutConnector(query, out var routed) && routed is not null)
        {
            connector = routed;
        }
        else
        {
            throw new InvalidStateException("No payout connector serves this corridor.");
        }

        if (!ConnectorSatisfies(connector, query))
        {
            throw new InvalidStateException(
                $"Payout connector '{connector.ProviderCode}' does not serve this beneficiary corridor.");
        }

        return connector;
    }

    private static bool ConnectorSatisfies(IPartnerConnector connector, PartnerConnectorQuery query)
        => connector.Capabilities.Any(capability =>
            capability.Category == query.Category
            && (query.Country is null
                || capability.Countries.Contains(query.Country, StringComparer.OrdinalIgnoreCase))
            && (query.Currency is null
                || capability.Currencies.Contains(query.Currency, StringComparer.OrdinalIgnoreCase))
            && (query.Method is null
                || capability.Methods.Contains(query.Method, StringComparer.OrdinalIgnoreCase)));

    private static async Task<string?> TryResolveAccountNameAsync(
        IPartnerPayoutConnector connector,
        VerifyPayoutBeneficiaryRequest request,
        string destinationType,
        string currency,
        CancellationToken cancellationToken)
    {
        if (!destinationType.Equals("Bank", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            var result = await connector.ResolveAccountAsync(
                new AccountResolutionRequest(
                    NormalizeRequired(request.BankCode, nameof(request.BankCode)),
                    NormalizeRequired(request.AccountNumber, nameof(request.AccountNumber)),
                    currency),
                cancellationToken);

            return result.Resolved ? Normalize(result.AccountName) : null;
        }
        catch (Exception ex) when (IsNameEnquiryFallback(ex))
        {
            return null;
        }
    }

    private static bool IsNameEnquiryFallback(Exception exception)
        => exception is TimeoutException
           || exception is InvalidOperationException
           || exception.GetType().Name == "FlutterwaveException";

    private async Task<(Guid BeneficiaryPartyId, string BeneficiaryName)> EnsurePartyGraphAsync(
        VerifyPayoutBeneficiaryRequest request,
        Guid? existingBeneficiaryPartyId,
        string accountName,
        CancellationToken cancellationToken)
    {
        Guid? beneficiaryPartyId = null;
        if (existingBeneficiaryPartyId is { } existing && existing != Guid.Empty)
        {
            beneficiaryPartyId = existing;
        }
        else if (request.BeneficiaryPartyId is { } supplied && supplied != Guid.Empty)
        {
            beneficiaryPartyId = supplied;
        }

        var saveShape = new SavePayoutBeneficiaryRequest(
            request.CustomerPartyId,
            request.DestinationType,
            accountName,
            request.Currency,
            "****0000",
            request.BankCode,
            request.BranchCode,
            request.MobileNetwork,
            BeneficiaryPartyId: beneficiaryPartyId,
            BeneficiaryDisplayName: request.BeneficiaryDisplayName,
            BeneficiaryPartyType: request.BeneficiaryPartyType,
            RelationshipTypeCode: request.RelationshipTypeCode,
            Notes: request.Notes);

        var (resolvedPartyId, beneficiaryName) = await ResolveBeneficiaryPartyAsync(saveShape, cancellationToken);

        await EnsureRelationshipAsync(
            request.CustomerPartyId,
            resolvedPartyId,
            request.RelationshipTypeCode,
            request.Notes,
            cancellationToken);

        await _partyService.AssignPartyRoleAsync(
            resolvedPartyId,
            PartyRoleCodes.Beneficiary,
            CustomerContextType,
            request.CustomerPartyId,
            cancellationToken);

        return (resolvedPartyId, beneficiaryName);
    }

    private async Task<string> ResolveBeneficiaryNameAsync(
        Guid? beneficiaryPartyId,
        string fallback,
        CancellationToken cancellationToken)
    {
        if (beneficiaryPartyId is null || beneficiaryPartyId.Value == Guid.Empty)
        {
            return fallback;
        }

        var party = await _partyService.GetPartyAsync(beneficiaryPartyId.Value, cancellationToken);
        return party?.DisplayName ?? fallback;
    }

    private static PayoutDestination BuildRawDestination(VerifyPayoutBeneficiaryRequest request, string destinationType)
        => destinationType.Equals("Bank", StringComparison.OrdinalIgnoreCase)
            ? new BankAccountDestination(
                NormalizeRequired(request.BankCode, nameof(request.BankCode)),
                NormalizeRequired(request.AccountNumber, nameof(request.AccountNumber)),
                Normalize(request.BranchCode),
                Normalize(request.AccountName))
        : destinationType.Equals("MobileMoney", StringComparison.OrdinalIgnoreCase)
            ? new MobileMoneyDestination(
                NormalizeRequired(request.MobileNetwork, nameof(request.MobileNetwork)),
                NormalizeRequired(request.Msisdn, nameof(request.Msisdn)),
                Normalize(request.AccountName))
        : destinationType.Equals("Wallet", StringComparison.OrdinalIgnoreCase)
            ? new WalletDestination(
                NormalizeRequired(request.WalletId, nameof(request.WalletId)),
                Normalize(request.AccountName))
        : throw new ArgumentException($"Unsupported payout destination type '{request.DestinationType}'.", nameof(request));

    private static string MaskDestination(PayoutDestination destination)
    {
        var value = destination switch
        {
            BankAccountDestination bank => bank.AccountNumber,
            MobileMoneyDestination mobile => mobile.PhoneNumber,
            WalletDestination wallet => wallet.WalletId,
            _ => string.Empty
        };

        var lastFour = value.Length <= 4 ? value : value[^4..];
        return $"****{lastFour}";
    }

    private static string BuildRailFingerprint(
        string providerCode,
        string country,
        string currency,
        string destinationType,
        PayoutDestination destination)
    {
        var normalized = destination switch
        {
            BankAccountDestination bank => string.Join('|',
                providerCode.Trim().ToUpperInvariant(),
                country.Trim().ToUpperInvariant(),
                currency.Trim().ToUpperInvariant(),
                destinationType.Trim().ToUpperInvariant(),
                bank.BankCode.Trim().ToUpperInvariant(),
                Normalize(bank.BranchCode)?.ToUpperInvariant() ?? string.Empty,
                bank.AccountNumber.Trim()),
            MobileMoneyDestination mobile => string.Join('|',
                providerCode.Trim().ToUpperInvariant(),
                country.Trim().ToUpperInvariant(),
                currency.Trim().ToUpperInvariant(),
                destinationType.Trim().ToUpperInvariant(),
                mobile.Network.Trim().ToUpperInvariant(),
                mobile.PhoneNumber.Trim()),
            WalletDestination wallet => string.Join('|',
                providerCode.Trim().ToUpperInvariant(),
                country.Trim().ToUpperInvariant(),
                currency.Trim().ToUpperInvariant(),
                destinationType.Trim().ToUpperInvariant(),
                wallet.WalletId.Trim()),
            _ => throw new ArgumentOutOfRangeException(nameof(destination), destination, null)
        };

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized))).ToLowerInvariant();
    }

    private static string NormalizeRail(string value)
    {
        var normalized = NormalizeRequired(value, nameof(value));
        return normalized.Equals("Bank", StringComparison.OrdinalIgnoreCase)
            ? "Bank"
            : normalized.Equals("MobileMoney", StringComparison.OrdinalIgnoreCase)
                ? "MobileMoney"
                : normalized.Equals("Wallet", StringComparison.OrdinalIgnoreCase)
                    ? "Wallet"
                    : throw new ArgumentException($"Unsupported payout destination type '{value}'.", nameof(value));
    }

    private static string NormalizeRequired(string? value, string paramName)
        => string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException($"{paramName} is required.", paramName)
            : value.Trim();

    private static PayoutBeneficiaryResponse ToResponse(
        ExternalPayoutAccount account,
        string beneficiaryName,
        string relationshipTypeCode)
        => new(
            account.Id,
            account.CustomerPartyId,
            account.BeneficiaryPartyId ?? Guid.Empty,
            beneficiaryName,
            account.DestinationType,
            account.MaskedAccountIdentifier,
            account.Currency,
            account.BankCode,
            account.MobileNetwork,
            account.AccountName,
            account.ProviderCode,
            relationshipTypeCode,
            account.IsVerified);

    private async Task<(Guid PartyId, string DisplayName)> ResolveBeneficiaryPartyAsync(
        SavePayoutBeneficiaryRequest request,
        CancellationToken cancellationToken)
    {
        if (request.BeneficiaryPartyId is { } existingPartyId && existingPartyId != Guid.Empty)
        {
            var party = await _partyService.GetPartyAsync(existingPartyId, cancellationToken)
                ?? throw new InvalidOperationException($"Beneficiary party {existingPartyId} not found.");

            return (party.PartyId, party.DisplayName);
        }

        var displayName = !string.IsNullOrWhiteSpace(request.BeneficiaryDisplayName)
            ? request.BeneficiaryDisplayName.Trim()
            : request.AccountName.Trim();

        var partyType = string.IsNullOrWhiteSpace(request.BeneficiaryPartyType)
            ? "Person"
            : request.BeneficiaryPartyType.Trim();

        var created = await _partyService.CreatePartyAsync(
            new CreatePartyRequest(displayName, partyType, null, null, null, null, null),
            cancellationToken);

        return (created.PartyId, created.DisplayName);
    }

    private async Task EnsureRelationshipAsync(
        Guid customerPartyId,
        Guid beneficiaryPartyId,
        string relationshipTypeCode,
        string? notes,
        CancellationToken cancellationToken)
    {
        var relationships = await _partyService.GetRelationshipsAsync(customerPartyId, cancellationToken);

        var alreadyLinked = relationships.Any(relationship =>
            relationship.FromPartyId == customerPartyId
            && relationship.ToPartyId == beneficiaryPartyId
            && string.Equals(relationship.RelationshipTypeCode, relationshipTypeCode, StringComparison.OrdinalIgnoreCase));

        if (alreadyLinked)
        {
            return;
        }

        await _partyService.CreateRelationshipAsync(
            new CreatePartyRelationshipRequest(customerPartyId, beneficiaryPartyId, relationshipTypeCode, notes),
            cancellationToken);
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
