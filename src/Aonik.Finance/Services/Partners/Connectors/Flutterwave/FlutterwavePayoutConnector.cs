using System.Globalization;

using Aonik.Finance.Contracts.Services.Partners.Connectors;
using Aonik.Finance.Services.Partners.Connectors.Flutterwave.Dtos;
using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Services.Partners.Connectors.Flutterwave;

/// <summary>
/// Real Flutterwave v4 payout connector (Spec 037). Maps the normalized payout port onto v4's
/// transfers surface: quote (<c>/transfers/rates</c>), name enquiry (<c>/banks/account-resolve</c>),
/// recipient registration (<c>/transfers/recipients</c>), and transfer dispatch via the
/// <c>recipient_id</c> flow (<c>/transfers</c>). Stays vendor-agnostic of OrderId — money-action
/// logging lives at the caller. Throws <see cref="FlutterwaveException"/> / <see cref="TimeoutException"/>
/// for transport failures; a business <c>FAILED</c> status is returned as a normalized result.
/// </summary>
internal sealed class FlutterwavePayoutConnector : IPartnerPayoutConnector
{
    private readonly FlutterwaveClient _client;
    private readonly IFlutterwaveConfigProvider _configProvider;
    private ConnectorBinding? _binding;

    public FlutterwavePayoutConnector(FlutterwaveClient client, IFlutterwaveConfigProvider configProvider)
    {
        _client = client;
        _configProvider = configProvider;
    }

    public string ProviderCode => "Flutterwave";

    /// <summary>The connector row this instance is bound to (Spec 042 §9); null for the legacy/global default.</summary>
    public Guid? ConnectorId => _binding?.ConnectorId;

    /// <summary>
    /// Binds this instance to a persisted <c>Connector</c> row so it resolves that account's credentials.
    /// Called by <see cref="Aonik.Finance.Services.Partners.Connectors.PartnerConnectorFactory"/>; the
    /// DI-registered instance stays unbound and resolves the legacy default.
    /// </summary>
    internal FlutterwavePayoutConnector Bind(ConnectorBinding binding)
    {
        _binding = binding;
        return this;
    }

    // In-memory capability lanes (Spec 037 §7.4): the resolver matches on these, not persisted
    // ConnectorCapability rows. Destination-oriented — the Payabo UK→NG wedge.
    public IReadOnlyCollection<PartnerConnectorCapability> Capabilities { get; } = new[]
    {
        new PartnerConnectorCapability(
            PartnerServiceCategory.Payout,
            new[] { "NG" },
            new[] { "NGN" },
            new[] { "Bank", "MobileMoney" }),
    };

    public async Task<PayoutQuoteResult> QuotePayoutAsync(
        PayoutQuoteRequest request, CancellationToken cancellationToken = default)
    {
        var options = await GetConfiguredOptionsAsync(cancellationToken);
        var sourceCurrency = request.Amount.Currency;          // the quoted amount is the SOURCE (debit) amount
        var destinationCurrency = request.DestinationCurrency; // the recipient receives this currency

        // Same-currency payout has no FX leg; Flutterwave cannot quote a transfer fee pre-send (G8).
        if (string.Equals(sourceCurrency, destinationCurrency, StringComparison.OrdinalIgnoreCase))
        {
            return new PayoutQuoteResult(null, null, null, new RawProviderResponse(null, "same-currency", null));
        }

        // Flutterwave's /transfers/rates is DESTINATION-driven: you give the amount the recipient
        // receives (destination.amount) and it returns the debit (source.amount) + rate, where rate is
        // SOURCE units per 1 DESTINATION unit. Our request carries a SOURCE amount, so we must NOT send
        // it as destination.amount (that asks the inverse question — the debit to deliver that many
        // destination units, off by orders of magnitude). Instead we read the rate and convert:
        // recipient amount = source / rate. The rate is amount-independent for a pair, so the nominal
        // destination.amount we send only serves to retrieve the rate.
        var body = new FwRateRequest
        {
            Source = new FwRateCurrency { Currency = sourceCurrency },
            Destination = new FwRateCurrency { Currency = destinationCurrency, Amount = request.Amount.Amount },
            Precision = 6,
        };

        var data = await PostDataAsync<FwRateData>(
            "/transfers/rates", body, FlutterwaveReferences.FreshIdempotencyKey(), options, cancellationToken);

        var sourcePerDestination = TryParseDecimal(data.Rate);
        Money? converted = null;
        decimal? fxRate = null;
        if (sourcePerDestination is > 0m)
        {
            // Recipient (destination) amount our source buys, and the intuitive destination-per-source rate.
            var recipientAmount = Math.Round(
                request.Amount.Amount / sourcePerDestination.Value, 2, MidpointRounding.AwayFromZero);
            converted = new Money(recipientAmount, destinationCurrency);
            fxRate = Math.Round(1m / sourcePerDestination.Value, 8, MidpointRounding.AwayFromZero);
        }

        // Fee is unavailable from /transfers/rates — null means "fee known only at execution" (§5.7).
        return new PayoutQuoteResult(null, fxRate, converted, new RawProviderResponse(data.Id, null, null));
    }

    public async Task<AccountResolutionResult> ResolveAccountAsync(
        AccountResolutionRequest request, CancellationToken cancellationToken = default)
    {
        var options = await GetConfiguredOptionsAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(request.Currency))
        {
            throw new ArgumentException(
                "Flutterwave account resolution requires a currency (the request is currency-discriminated).",
                nameof(request));
        }

        var body = new FwAccountResolveRequest
        {
            Currency = request.Currency.Trim().ToUpperInvariant(),
            Account = new FwAccountRef { Code = request.BankCode, Number = request.AccountNumber },
        };

        var data = await PostDataAsync<FwAccountResolveData>(
            "/banks/account-resolve", body, FlutterwaveReferences.FreshIdempotencyKey(), options, cancellationToken);

        var resolved = !string.IsNullOrWhiteSpace(data.AccountName);
        return new AccountResolutionResult(resolved, data.AccountName, new RawProviderResponse(null, null, null));
    }

    public async Task<RecipientRegistrationResult> RegisterRecipientAsync(
        RecipientRegistrationRequest request, CancellationToken cancellationToken = default)
    {
        var options = await GetConfiguredOptionsAsync(cancellationToken);
        var currency = request.Currency.Trim().ToUpperInvariant();
        var (first, last) = SplitName(request.AccountName);
        var name = new FwName { First = first, Last = last };

        var body = request.Destination switch
        {
            BankAccountDestination bank => new FwRecipientRequest
            {
                Type = $"bank_{currency.ToLowerInvariant()}",
                Name = name,
                Bank = new FwRecipientBank { Code = bank.BankCode, AccountNumber = bank.AccountNumber },
            },
            MobileMoneyDestination mobile => new FwRecipientRequest
            {
                Type = $"mobile_money_{currency.ToLowerInvariant()}",
                Name = name,
                MobileMoney = new FwRecipientMobileMoney
                {
                    Network = mobile.Network,
                    Msisdn = mobile.PhoneNumber,
                    Country = request.Country,
                },
            },
            WalletDestination wallet => new FwRecipientRequest
            {
                Type = "wallet",
                Name = name,
                Wallet = new FwRecipientWallet { Provider = "flutterwave", Identifier = wallet.WalletId },
            },
            _ => throw new ArgumentException(
                $"Unsupported payout destination type '{request.Destination.GetType().Name}'.", nameof(request)),
        };

        var idempotencyKey = FlutterwaveReferences.IdempotencyKeyFrom(
            $"{request.Currency}:{DescribeDestination(request.Destination)}");

        var data = await PostDataAsync<FwRecipientData>(
            "/transfers/recipients", body, idempotencyKey, options, cancellationToken);

        var registered = !string.IsNullOrWhiteSpace(data.Id);
        return new RecipientRegistrationResult(
            registered, data.Id, request.AccountName, new RawProviderResponse(data.Type, null, null));
    }

    public async Task<PayoutInitiationResult> InitiatePayoutAsync(
        PayoutInstruction instruction, CancellationToken cancellationToken = default)
    {
        var options = await GetConfiguredOptionsAsync(cancellationToken);
        var recipientId = GetRecipientId(instruction.Destination);

        var transferPurpose = instruction.Metadata is not null
            && instruction.Metadata.TryGetValue("transfer_purpose", out var purpose)
            && !string.IsNullOrWhiteSpace(purpose)
                ? purpose
                : options.DefaultTransferPurpose;

        var body = new FwTransferRequest
        {
            Action = "instant",
            Reference = FlutterwaveReferences.SanitizeReference(instruction.ClientReference),
            Narration = instruction.Narration,
            TransferPurpose = transferPurpose,
            PaymentInstruction = new FwPaymentInstruction
            {
                SourceCurrency = instruction.DebitCurrency,
                DestinationCurrency = instruction.Amount.Currency,
                Amount = new FwAmount { Value = instruction.Amount.Amount, AppliesTo = "destination_currency" },
                RecipientId = recipientId,
            },
            // Carry our reference so the webhook translator can reconstruct the exact stored value (G17).
            Meta = new Dictionary<string, string> { ["aonik_client_reference"] = instruction.ClientReference },
        };

        var idempotencyKey = FlutterwaveReferences.IdempotencyKeyFrom(instruction.ClientReference);
        var data = await PostDataAsync<FwTransferData>("/transfers", body, idempotencyKey, options, cancellationToken);

        return new PayoutInitiationResult(
            new PartnerReference(instruction.ClientReference, data.Id),
            MapTransferStatus(data.Status),
            ToMoney(data.Fee),
            BuildRaw(data));
    }

    public async Task<PayoutStatusResult> GetPayoutStatusAsync(
        PartnerReference reference, CancellationToken cancellationToken = default)
    {
        var options = await GetConfiguredOptionsAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(reference.ProviderReference))
        {
            throw new ArgumentException(
                "A provider reference (transfer id) is required to query Flutterwave transfer status.",
                nameof(reference));
        }

        var envelope = await _client.GetAsync<FwEnvelope<FwTransferData>>(
            $"/transfers/{reference.ProviderReference}", options, cancellationToken);
        var data = envelope.Data ?? throw new FlutterwaveException(
            "Flutterwave transfer status response had no data.", "EMPTY", null, null, retryable: false);

        return new PayoutStatusResult(
            new PartnerReference(reference.ClientReference, data.Id ?? reference.ProviderReference),
            MapTransferStatus(data.Status),
            ToMoney(data.Fee),
            BuildRaw(data));
    }

    private async Task<T> PostDataAsync<T>(
        string path, object body, string idempotencyKey, FlutterwaveOptions options, CancellationToken cancellationToken)
        where T : class
    {
        var envelope = await _client.PostAsync<FwEnvelope<T>>(path, body, idempotencyKey, options, cancellationToken);
        return envelope.Data
            ?? throw new FlutterwaveException(
                $"Flutterwave response for '{path}' had no data.", "EMPTY", null, null, retryable: false);
    }

    private async Task<FlutterwaveOptions> GetConfiguredOptionsAsync(CancellationToken cancellationToken)
    {
        var options = _binding is null
            ? await _configProvider.GetAsync(cancellationToken)
            : await _configProvider.GetAsync(_binding, cancellationToken);
        if (!options.IsConfigured())
        {
            throw new FlutterwaveException(
                "Flutterwave is not configured or disabled.",
                errorType: "CONFIGURATION",
                errorCode: null,
                statusCode: null,
                retryable: false);
        }

        return options;
    }

    private static string GetRecipientId(PayoutDestination destination)
    {
        var value = destination switch
        {
            BankAccountDestination bank => bank.AccountNumber,
            MobileMoneyDestination mobile => mobile.PhoneNumber,
            WalletDestination wallet => wallet.WalletId,
            _ => string.Empty,
        };

        // The shipped caller passes the connector's stored recipient id (a Flutterwave resource id
        // such as "rcb_B9aAgsdzzl") in the account field (Spec 037 G19). A masked identifier or raw
        // account number is not a recipient id and cannot be paid — fail closed with a clear message.
        // Match the Flutterwave resource-id SHAPE, not a single prefix: recipient ids vary by rail
        // (bank = rcb_, others differ), so a hardcoded "rcp_" check would reject valid beneficiaries.
        if (!IsLikelyRecipientId(value))
        {
            throw new FlutterwaveException(
                "Payout destination has no Flutterwave recipient id (e.g. rcb_…); register the beneficiary "
                + "via RegisterRecipientAsync before dispatch (Spec 037 G19/G20).",
                errorType: "NO_RECIPIENT",
                errorCode: null,
                statusCode: null,
                retryable: false);
        }

        return value;
    }

    // A Flutterwave recipient id is a resource id like "rcb_B9aAgsdzzl": a lowercase token, an
    // underscore, then alphanumerics. This accepts every recipient-type prefix while rejecting masked
    // identifiers ("****1234") and raw account numbers / MSISDNs (which contain no underscore).
    private static bool IsLikelyRecipientId(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length <= 4 || !value.Contains('_'))
        {
            return false;
        }

        foreach (var c in value)
        {
            if (!char.IsLetterOrDigit(c) && c != '_')
            {
                return false;
            }
        }

        return true;
    }

    private static PartnerTransactionStatus MapTransferStatus(string? status)
        => status?.Trim().ToUpperInvariant() switch
        {
            "NEW" or "INITIATED" => PartnerTransactionStatus.Pending,
            "PENDING" => PartnerTransactionStatus.Processing,
            "SUCCESSFUL" => PartnerTransactionStatus.Succeeded,
            "FAILED" => PartnerTransactionStatus.Failed,
            "CANCELLED" => PartnerTransactionStatus.Failed,
            _ => PartnerTransactionStatus.Unknown,
        };

    private static Money? ToMoney(FwMoney? money)
        => money?.Value is { } value && !string.IsNullOrWhiteSpace(money.Currency)
            ? new Money(value, money.Currency)
            : null;

    // Redacted raw — vendor status + code/message only, never account data (Spec 031 sensitive-data rule).
    private static RawProviderResponse BuildRaw(FwTransferData data)
        => new(data.Status, data.ProviderResponse?.Message ?? data.ProviderResponse?.Code, null);

    private static (string First, string Last) SplitName(string accountName)
    {
        var trimmed = (accountName ?? string.Empty).Trim();
        var spaceIndex = trimmed.IndexOf(' ');
        return spaceIndex < 0
            ? (trimmed, string.Empty)
            : (trimmed[..spaceIndex], trimmed[(spaceIndex + 1)..].Trim());
    }

    private static string DescribeDestination(PayoutDestination destination)
        => destination switch
        {
            BankAccountDestination bank => $"bank:{bank.BankCode}:{bank.AccountNumber}",
            MobileMoneyDestination mobile => $"momo:{mobile.Network}:{mobile.PhoneNumber}",
            WalletDestination wallet => $"wallet:{wallet.WalletId}",
            _ => destination.GetType().Name,
        };

    private static decimal? TryParseDecimal(string? value)
        => decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result)
            ? result
            : null;
}
