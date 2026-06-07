using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using Aonik.Finance.Contracts.Models.Remittance;
using Aonik.Finance.Contracts.Services.Partners.Connectors;
using Aonik.Finance.Contracts.Services.Pricing;
using Aonik.Finance.Contracts.Services.Remittance;
using Aonik.Finance.Entities.Orders;
using Aonik.Finance.Entities.Partners;
using Aonik.Finance.Entities.Payments;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.Ledger;
using Aonik.Finance.Services.Observability;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Primitives;

using PricingModels = Aonik.Finance.Contracts.Models.Pricing;

// PartnerWebhookEvent exists twice — the persisted entity (Entities.Partners) and the translator's
// normalized record (Contracts...Connectors). Alias the entity; the record is only ever used via var.
using PartnerWebhookEventRow = Aonik.Finance.Entities.Partners.PartnerWebhookEvent;

namespace Aonik.Finance.Services.Remittance;

/// <summary>
/// Payabo B2C remittance orchestration over the shipped order / pricing / ledger / payout /
/// transmission / webhook primitives (Spec 036). Confirm follows the hard ordering invariant: lock
/// the quote and post the customer debit BEFORE any external connector call, and never hold a SQL
/// transaction open across that call.
/// </summary>
internal sealed class RemittanceOrderService : IRemittanceOrderService
{
    private const string RemittanceOrderType = "Remittance";
    private const string RemittanceQuoteType = "Remittance";
    private const string RemittanceItemType = "RemittancePayout";
    private const string DefaultServiceCode = "REMITTANCE.PAYOUT";
    private const string SimulatedProviderCode = "Simulated";
    private const string DefaultNarration = "Payabo remittance";

    // Item-level fulfilment states (Spec 036 §4.1).
    private const string ItemQuoteLocked = "QuoteLocked";
    private const string ItemTransmitted = "Transmitted";
    private const string ItemSettled = "Settled";
    private const string ItemFailed = "Failed";

    // Candidate payout rails probed against registered connector capabilities to advertise the
    // supported destination methods for a corridor.
    private static readonly string[] CandidateDestinationMethods = { "Bank", "MobileMoney", "Wallet" };

    private readonly FinanceDbContext _db;
    private readonly IPricingService _pricingService;
    private readonly IPartnerConnectorResolver _connectorResolver;
    private readonly LedgerPostingService _ledgerPostingService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IConfiguration _configuration;
    private readonly IClock _clock;
    private readonly ILogger<RemittanceOrderService> _logger;

    public RemittanceOrderService(
        FinanceDbContext db,
        IPricingService pricingService,
        IPartnerConnectorResolver connectorResolver,
        LedgerPostingService ledgerPostingService,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider,
        IConfiguration configuration,
        IClock clock,
        ILogger<RemittanceOrderService> logger)
    {
        _db = db;
        _pricingService = pricingService;
        _connectorResolver = connectorResolver;
        _ledgerPostingService = ledgerPostingService;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
        _configuration = configuration;
        _clock = clock;
        _logger = logger;
    }

    // ── Quote ────────────────────────────────────────────────────────────────
    public async Task<RemittanceQuoteResponse> QuoteAsync(
        RemittanceQuoteRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateQuoteRequest(request);

        var tenantId = _tenantProvider.GetCurrentTenantId();
        await EnsurePartyInTenantAsync(request.CustomerPartyId, cancellationToken);

        var originCountry = Normalize(request.OriginCountry);
        var destinationCountry = Normalize(request.DestinationCountry);
        var originCurrency = Normalize(request.OriginCurrency);
        var destinationCurrency = Normalize(request.DestinationCurrency);
        var serviceCode = string.IsNullOrWhiteSpace(request.ServiceCode)
            ? DefaultServiceCode
            : request.ServiceCode.Trim().ToUpperInvariant();

        // Reuse the pricing engine; it persists the quote with QuoteType = "Remittance" and emits the
        // Quote-stage money-action logs (#142).
        var pricingRequest = new PricingModels.PricingQuoteRequest(
            originCurrency,
            destinationCurrency,
            originCountry,
            destinationCountry,
            serviceCode,
            request.DestinationAmount,
            request.OriginAmount,
            request.CustomerPartyId,
            request.CustomerTier,
            RemittanceQuoteType);

        var pricing = await _pricingService.GetRemittanceQuoteAsync(pricingRequest, cancellationToken);

        // Read the persisted quote back for its expiry (the pricing response does not carry it).
        var quote = await _db.PricingQuotes
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == pricing.PricingQuoteId, cancellationToken)
            ?? throw new InvalidOperationException("Remittance quote could not be persisted.");

        var supportedMethods = ResolveSupportedDestinationMethods(destinationCountry, destinationCurrency);

        return new RemittanceQuoteResponse(
            pricing.PricingQuoteId,
            RemittanceQuoteType,
            originCountry,
            destinationCountry,
            originCurrency,
            destinationCurrency,
            pricing.OriginAmount,
            pricing.DestinationAmount,
            pricing.FeesTotal,
            pricing.TotalAmount,
            pricing.ExchangeRate,
            pricing.RateMarkup,
            pricing.PricingPolicyId,
            pricing.PricingPolicyVersion,
            pricing.FxRateId,
            pricing.RateTimestamp,
            new DateTimeOffset(DateTime.SpecifyKind(quote.ExpiresAt, DateTimeKind.Utc)),
            pricing.FeeBreakdown,
            supportedMethods);
    }

    // ── Confirm ──────────────────────────────────────────────────────────────
    public async Task<RemittanceOrderResponse> ConfirmAsync(
        ConfirmRemittanceRequest request,
        string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new ArgumentException("An Idempotency-Key is required to confirm a remittance.", nameof(idempotencyKey));
        }

        var key = idempotencyKey.Trim();
        var tenantId = _tenantProvider.GetCurrentTenantId();

        using var activity = FinanceActivitySource.Source.StartActivity("remittance.confirm");
        activity?.SetTag(FinanceActivitySource.StageTag, MoneyActionStages.Confirm);
        activity?.SetTag(FinanceActivitySource.TenantIdTag, tenantId);

        // Idempotency: replaying the same key returns the existing order without re-executing.
        var existing = await _db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(
                o => o.OrderType == RemittanceOrderType && o.IdempotencyKey == key,
                cancellationToken);

        if (existing is not null)
        {
            activity?.SetTag(FinanceActivitySource.OrderIdTag, existing.Id);
            activity?.SetTag(FinanceActivitySource.OutcomeTag, MoneyActionOutcomes.SkippedIdempotent);
            return await BuildResponseAsync(existing, cancellationToken);
        }

        // Load + validate the locked inputs.
        var quote = await _db.PricingQuotes
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == request.PricingQuoteId, cancellationToken)
            ?? throw new InvalidOperationException("Pricing quote not found.");

        if (!string.Equals(quote.QuoteType, RemittanceQuoteType, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Pricing quote is not a remittance quote.");
        }

        if (quote.ExpiresAt <= _clock.UtcNow)
        {
            _logger.OrderRejected(Guid.Empty, tenantId, "Remittance quote expired.");
            throw new InvalidOperationException("Remittance quote has expired.");
        }

        if (quote.CustomerId.HasValue && quote.CustomerId.Value != request.CustomerPartyId)
        {
            throw new InvalidOperationException("Pricing quote does not belong to the requested customer.");
        }

        var account = await _db.ExternalPayoutAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == request.DestinationExternalAccountId, cancellationToken)
            ?? throw new InvalidOperationException("Destination payout account not found.");

        if (account.CustomerPartyId != request.CustomerPartyId)
        {
            throw new InvalidOperationException("Destination payout account is not owned by the customer.");
        }

        if (!string.IsNullOrWhiteSpace(account.Currency)
            && !string.Equals(account.Currency, quote.DestinationCurrency, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Destination payout account currency does not match the quote.");
        }

        // Reject unsupported rails up front — before any order/debit — rather than silently transmitting
        // them on the wrong rail. Saved destination types are free-form trimmed text, so this (and the
        // instruction builder) compare case-insensitively.
        if (!IsSupportedDestinationRail(account.DestinationType))
        {
            throw new InvalidOperationException(
                $"Unsupported payout destination type '{account.DestinationType}'.");
        }

        // Resolve the payout connector route. Prefer an explicit provider, then capability routing,
        // then the simulated fallback (Spec 036 §6.4 step 5).
        var (connector, providerCode) = ResolvePayoutConnector(request.ProviderCode, quote, account.DestinationType);
        // The simulated connector is keyed by ProviderCode and has no Connector row; a persistent
        // ProviderCode → Connector.Id mapping is a follow-up. Guid.Empty marks "unmapped" today.
        var connectorId = Guid.Empty;

        var now = _clock.UtcNow;
        var orderId = Guid.NewGuid();
        var orderItemId = Guid.NewGuid();
        var payoutId = Guid.NewGuid();
        var clientReference = $"REM-{orderId:N}";

        using var orderScope = _logger.BeginOrderScope(orderId);
        activity?.SetTag(FinanceActivitySource.OrderIdTag, orderId);
        activity?.SetTag(FinanceActivitySource.PricingQuoteIdTag, quote.Id);

        var details = new RemittanceOrderDetails(
            quote.Id,
            request.CustomerPartyId,
            account.BeneficiaryPartyId,
            account.Id,
            account.DestinationType,
            account.MaskedAccountIdentifier,
            quote.OriginCountry,
            quote.DestinationCountry,
            quote.OriginCurrency,
            quote.DestinationCurrency,
            quote.OriginAmount,
            quote.DestinationAmount,
            quote.FeesTotal,
            quote.TotalAmount,
            quote.ExchangeRate,
            quote.RateMarkup,
            quote.PricingPolicyId,
            quote.PricingPolicyVersion,
            quote.ExpiresAt,
            request.PurposeCode,
            request.Narration,
            connectorId,
            providerCode);

        var detailsJson = JsonSerializer.Serialize(details);

        var order = new Order
        {
            Id = orderId,
            TenantId = tenantId,
            OrderType = RemittanceOrderType,
            IdempotencyKey = key,
            PayerPartyId = request.CustomerPartyId,
            PurposeCode = request.PurposeCode,
            OriginCountry = quote.OriginCountry,
            DestinationCountry = quote.DestinationCountry,
            AmountIn = quote.OriginAmount,
            CurrencyIn = quote.OriginCurrency,
            AmountOut = quote.DestinationAmount,
            CurrencyOut = quote.DestinationCurrency,
            FeesJson = JsonSerializer.Serialize(new[]
            {
                new { Code = "REMITTANCE_FEES", Amount = quote.FeesTotal, Currency = quote.OriginCurrency }
            }),
            FxQuoteId = quote.FxRateId,
            Status = OrderStatuses.Pending,
            ProvenanceJson = detailsJson,
            Items =
            {
                new OrderItem
                {
                    Id = orderItemId,
                    TenantId = tenantId,
                    OrderId = orderId,
                    ItemType = RemittanceItemType,
                    ItemIndex = 0,
                    Status = ItemQuoteLocked,
                    ReceiverPartyId = account.BeneficiaryPartyId,
                    AmountIn = quote.OriginAmount,
                    CurrencyIn = quote.OriginCurrency,
                    AmountOut = quote.DestinationAmount,
                    CurrencyOut = quote.DestinationCurrency,
                    FeesTotal = quote.FeesTotal,
                    PricingQuoteId = quote.Id,
                    DetailsJson = detailsJson
                }
            },
            PartyRoles =
            {
                new OrderPartyRole
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    OrderId = orderId,
                    PartyId = request.CustomerPartyId,
                    Role = OrderPartyRoles.Payer
                }
            },
            HistoryEvents =
            {
                new OrderHistoryEvent
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    OrderId = orderId,
                    EventType = "RemittanceConfirmed",
                    EventAt = now,
                    ActorType = "User",
                    ActorId = _currentUserProvider.GetCurrentUserId() ?? Guid.Empty,
                    DetailsJson = "{}"
                }
            }
        };

        if (account.BeneficiaryPartyId.HasValue)
        {
            order.PartyRoles.Add(new OrderPartyRole
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                OrderId = orderId,
                PartyId = account.BeneficiaryPartyId.Value,
                Role = OrderPartyRoles.Receiver
            });
        }

        var payout = new Payout
        {
            Id = payoutId,
            TenantId = tenantId,
            Amount = quote.DestinationAmount,
            Currency = quote.DestinationCurrency,
            DestinationExternalAccountId = account.Id,
            PartnerId = account.PartnerId,
            ConnectorId = connectorId,
            ClientReference = clientReference,
            ProviderReference = string.Empty,
            DebitCurrency = quote.OriginCurrency,
            FxRate = quote.ExchangeRate,
            Fee = quote.FeesTotal,
            FeeCurrency = quote.OriginCurrency,
            Narration = request.Narration ?? DefaultNarration,
            DestinationType = account.DestinationType,
            OrderItemId = orderItemId,
            Status = PartnerTransactionStatus.Pending.ToString()
        };

        var fulfilmentRef = new OrderFulfilmentRef
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrderId = orderId,
            PayoutId = payoutId
        };

        _db.Orders.Add(order);
        _db.Payouts.Add(payout);
        _db.OrderFulfilmentRefs.Add(fulfilmentRef);
        await _db.SaveChangesAsync(cancellationToken);

        // Ordering invariant: post the customer debit BEFORE the connector call. If it fails, mark the
        // order failed and do NOT instruct the partner.
        try
        {
            await _ledgerPostingService.PostRemittanceDebitAsync(
                tenantId, orderId, quote.TotalAmount, quote.OriginCurrency, cancellationToken);
        }
        catch (Exception ex)
        {
            order.Status = OrderStatuses.Failed;
            order.Items[0].Status = ItemFailed;
            await _db.SaveChangesAsync(cancellationToken);
            activity?.SetTag(FinanceActivitySource.OutcomeTag, MoneyActionOutcomes.Failed);
            _logger.OrderRejected(orderId, tenantId, $"Remittance debit failed: {ex.Message}");
            throw;
        }

        _logger.OrderConfirmed(orderId, tenantId, RemittanceOrderType, quote.Id);

        // The pre-dispatch state is committed; the connector call is made outside any DB transaction.
        var instruction = BuildPayoutInstruction(clientReference, quote, account, request, tenantId, orderId, orderItemId, payoutId);

        PartnerTransactionStatus resultStatus;
        PartnerReference? reference = null;
        RawProviderResponse? raw = null;
        string? failure = null;

        try
        {
            var result = await connector.InitiatePayoutAsync(instruction, cancellationToken);
            resultStatus = result.Status;
            reference = result.Reference;
            raw = result.Raw;
        }
        catch (Exception ex)
        {
            resultStatus = PartnerTransactionStatus.Failed;
            failure = ex.Message;
            _logger.PaymentTransmitFailed(orderId, tenantId, providerCode, ex.Message, ex);
        }

        await ApplyConnectorResultAsync(
            order, payout, connectorId, clientReference, resultStatus, reference, raw, failure, providerCode, now, cancellationToken);

        activity?.SetTag(FinanceActivitySource.OutcomeTag,
            order.Status == OrderStatuses.Failed ? MoneyActionOutcomes.Failed : MoneyActionOutcomes.Success);

        return await BuildResponseAsync(order, cancellationToken);
    }

    // ── Get ──────────────────────────────────────────────────────────────────
    public async Task<RemittanceOrderResponse?> GetAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        // Tenant-scoped by the global query filter. Per-customer ownership scoping (Spec 036 §10.3) is
        // a follow-up once the endpoint resolves the current user's customer party.
        var order = await _db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(
                o => o.Id == orderId && o.OrderType == RemittanceOrderType,
                cancellationToken);

        if (order is null)
        {
            return null;
        }

        return await BuildResponseAsync(order, cancellationToken);
    }

    // ── Webhook settlement ─────────────────────────────────────────────────────
    public async Task ProcessWebhookAsync(
        PartnerWebhookEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        using var activity = FinanceActivitySource.Source.StartActivity("remittance.webhook");
        activity?.SetTag(FinanceActivitySource.StageTag, MoneyActionStages.Webhook);

        var providerCode = envelope.ProviderCode.Trim();
        var payloadHash = ComputePayloadHash(envelope.Body);

        IPartnerWebhookTranslator translator;
        try
        {
            translator = _connectorResolver.ResolveWebhookTranslator(providerCode);
        }
        catch (InvalidOperationException)
        {
            await StoreUntranslatableEventAsync(providerCode, payloadHash, envelope.Body, cancellationToken);
            return;
        }

        var signingSecret = ResolveWebhookSigningSecret(providerCode);
        var signatureValid = !string.IsNullOrEmpty(signingSecret)
            && translator.VerifySignature(envelope, signingSecret);

        var translated = translator.Translate(envelope);

        // Dedupe inbox: a row keyed by (ProviderCode, PayloadHash) that is already Processed short-circuits.
        var existingEvent = await _db.PartnerWebhookEvents
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                e => e.ProviderCode == providerCode && e.PayloadHash == payloadHash,
                cancellationToken);

        if (existingEvent is { ProcessingStatus: "Processed" })
        {
            return;
        }

        var now = _clock.UtcNow;
        var inboxRow = existingEvent ?? new PartnerWebhookEventRow
        {
            Id = Guid.NewGuid(),
            ProviderCode = providerCode,
            Category = translated.Category.ToString(),
            EventType = translated.EventType,
            ProviderReference = translated.Reference.ProviderReference ?? string.Empty,
            ClientReference = translated.Reference.ClientReference,
            PayloadHash = payloadHash,
            RawPayload = Redact(translated.Raw),
            SignatureValid = signatureValid,
            ReceivedAt = now,
            ProcessingStatus = "Received"
        };

        if (existingEvent is null)
        {
            _db.PartnerWebhookEvents.Add(inboxRow);
        }

        // Untrusted callbacks are stored for audit but never mutate financial state (Spec 036 §11).
        if (!signatureValid)
        {
            inboxRow.ProcessingStatus = "Failed";
            inboxRow.Error = "Invalid or missing signature.";
            await _db.SaveChangesAsync(cancellationToken);
            _logger.WebhookRejected(Guid.Empty, inboxRow.TenantId ?? Guid.Empty, providerCode, "Invalid signature.");
            return;
        }

        // Only payout events settle remittances here; other categories are handled elsewhere.
        if (translated.Category != PartnerServiceCategory.Payout)
        {
            inboxRow.ProcessingStatus = "Processed";
            inboxRow.ProcessedAt = now;
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        // Locate the payout across tenants (webhooks carry no authenticated tenant). ClientReference
        // (REM-{orderId:N}) is our own globally-unique, provider-agnostic key — the authoritative match.
        // ProviderReference is provider-scoped and can collide across providers, so it is only a
        // fallback, disambiguated by the remittance's stored provider.
        var payout = await LocateRemittancePayoutAsync(providerCode, translated.Reference, cancellationToken);

        if (payout is null)
        {
            inboxRow.ProcessingStatus = "Failed";
            inboxRow.Error = "No matching payout.";
            await _db.SaveChangesAsync(cancellationToken);
            _logger.WebhookRejected(Guid.Empty, inboxRow.TenantId ?? Guid.Empty, providerCode, "No matching payout.");
            return;
        }

        var item = payout.OrderItemId is null
            ? null
            : await _db.OrderItems.IgnoreQueryFilters().FirstOrDefaultAsync(i => i.Id == payout.OrderItemId, cancellationToken);
        var order = item is null
            ? null
            : await _db.Orders.IgnoreQueryFilters().FirstOrDefaultAsync(o => o.Id == item.OrderId, cancellationToken);

        if (order is null || item is null || !string.Equals(order.OrderType, RemittanceOrderType, StringComparison.OrdinalIgnoreCase))
        {
            inboxRow.ProcessingStatus = "Failed";
            inboxRow.Error = "Payout is not a remittance order.";
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        // Defense in depth: never settle/reverse a payout whose remittance was sent via a different
        // provider than the webhook claims to be from (provider references are provider-scoped).
        var details = TryDeserializeDetails(item.DetailsJson);
        if (details is not null
            && !string.Equals(details.ProviderCode, providerCode, StringComparison.OrdinalIgnoreCase))
        {
            inboxRow.ProcessingStatus = "Failed";
            inboxRow.Error = "Webhook provider does not match the remittance provider.";
            await _db.SaveChangesAsync(cancellationToken);
            _logger.WebhookRejected(order.Id, payout.TenantId, providerCode, "Provider mismatch.");
            return;
        }

        inboxRow.TenantId = payout.TenantId;
        _logger.WebhookReceived(order.Id, payout.TenantId, providerCode, translated.EventType);

        await SettleFromStatusAsync(order, item, payout, translated.Status, translated.Reference.ProviderReference, now, cancellationToken);

        inboxRow.ProcessingStatus = "Processed";
        inboxRow.ProcessedAt = now;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.WebhookProcessed(order.Id, payout.TenantId, providerCode, translated.EventType, MoneyActionOutcomes.Success);
        activity?.SetTag(FinanceActivitySource.OrderIdTag, order.Id);
        activity?.SetTag(FinanceActivitySource.OutcomeTag, MoneyActionOutcomes.Success);
    }

    // ── Internals ──────────────────────────────────────────────────────────────

    private async Task ApplyConnectorResultAsync(
        Order order,
        Payout payout,
        Guid connectorId,
        string clientReference,
        PartnerTransactionStatus status,
        PartnerReference? reference,
        RawProviderResponse? raw,
        string? failure,
        string providerCode,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var item = order.Items[0];
        payout.Status = status.ToString();
        payout.ProviderReference = reference?.ProviderReference ?? string.Empty;

        var transmission = new Transmission
        {
            Id = Guid.NewGuid(),
            TenantId = order.TenantId,
            PayoutId = payout.Id,
            ConnectorId = connectorId,
            IdempotencyKey = clientReference,
            ProviderReference = reference?.ProviderReference,
            Status = status.ToString(),
            RetryCount = 0,
            LastError = failure,
            RawResponseJson = raw is null ? null : Redact(raw)
        };
        _db.Transmissions.Add(transmission);

        var (orderStatus, itemStatus, settle, reverse, _) = MapResult(status);
        order.Status = orderStatus;
        item.Status = itemStatus;

        await _db.SaveChangesAsync(cancellationToken);

        if (settle)
        {
            // Synchronous terminal success: settle inline (idempotent). A later webhook for the same
            // payout is a no-op via ledger source-type idempotency.
            await _ledgerPostingService.PostRemittanceSettlementAsync(
                order.TenantId, payout.Id, order.Id, order.AmountIn + payout.Fee.GetValueOrDefault(), order.CurrencyIn, cancellationToken);
            _logger.PaymentTransmitted(order.Id, order.TenantId, providerCode, payout.ProviderReference);
        }
        else if (reverse)
        {
            await _ledgerPostingService.PostRemittanceFailureReversalAsync(
                order.TenantId, payout.Id, order.Id, order.AmountIn + payout.Fee.GetValueOrDefault(), order.CurrencyIn, cancellationToken);
        }
        else if (failure is null)
        {
            // Non-terminal: instruction accepted, awaiting settlement webhook.
            _logger.PaymentTransmitted(order.Id, order.TenantId, providerCode, payout.ProviderReference);
        }
    }

    private async Task SettleFromStatusAsync(
        Order order,
        OrderItem item,
        Payout payout,
        PartnerTransactionStatus status,
        string? providerReference,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(providerReference))
        {
            payout.ProviderReference = providerReference;
        }

        var (orderStatus, itemStatus, settle, reverse, terminal) = MapResult(status);
        if (!terminal)
        {
            return;
        }

        payout.Status = status.ToString();
        order.Status = orderStatus;
        item.Status = itemStatus;

        var amount = order.AmountIn + payout.Fee.GetValueOrDefault();
        if (settle)
        {
            await _ledgerPostingService.PostRemittanceSettlementAsync(
                payout.TenantId, payout.Id, order.Id, amount, order.CurrencyIn, cancellationToken);
        }
        else if (reverse)
        {
            await _ledgerPostingService.PostRemittanceFailureReversalAsync(
                payout.TenantId, payout.Id, order.Id, amount, order.CurrencyIn, cancellationToken);
        }
    }

    private static (string OrderStatus, string ItemStatus, bool Settle, bool Reverse, bool Terminal) MapResult(
        PartnerTransactionStatus status) => status switch
    {
        PartnerTransactionStatus.Succeeded => (OrderStatuses.Complete, ItemSettled, true, false, true),
        PartnerTransactionStatus.Failed
            or PartnerTransactionStatus.Reversed
            or PartnerTransactionStatus.Expired => (OrderStatuses.Failed, ItemFailed, false, true, true),
        _ => (OrderStatuses.Transmitted, ItemTransmitted, false, false, false)
    };

    private (IPartnerPayoutConnector Connector, string ProviderCode) ResolvePayoutConnector(
        string? requestedProviderCode,
        Entities.Pricing.PricingQuote quote,
        string destinationType)
    {
        if (!string.IsNullOrWhiteSpace(requestedProviderCode))
        {
            var byCode = _connectorResolver.ResolvePayoutConnector(requestedProviderCode.Trim());
            return (byCode, byCode.ProviderCode);
        }

        var query = new PartnerConnectorQuery(
            PartnerServiceCategory.Payout, quote.DestinationCountry, quote.DestinationCurrency, destinationType);

        if (_connectorResolver.TryResolvePayoutConnector(query, out var routed) && routed is not null)
        {
            return (routed, routed.ProviderCode);
        }

        var fallback = _connectorResolver.ResolvePayoutConnector(SimulatedProviderCode);
        return (fallback, fallback.ProviderCode);
    }

    private static PayoutInstruction BuildPayoutInstruction(
        string clientReference,
        Entities.Pricing.PricingQuote quote,
        ExternalPayoutAccount account,
        ConfirmRemittanceRequest request,
        Guid tenantId,
        Guid orderId,
        Guid orderItemId,
        Guid payoutId)
    {
        // We never hold a raw account number / MSISDN — pass the connector's reusable beneficiary token
        // (or the masked identifier as a last resort). Real rails resolve from VaultRef at execution.
        var reference = string.IsNullOrWhiteSpace(account.ProviderBeneficiaryId)
            ? account.MaskedAccountIdentifier
            : account.ProviderBeneficiaryId!;

        // Case-insensitive: saved rails are free-form trimmed text. Unsupported types are rejected at
        // confirm time, so reaching the throw here would be a logic error, not bad input.
        var rail = account.DestinationType.Trim();
        PayoutDestination destination =
            rail.Equals("Bank", StringComparison.OrdinalIgnoreCase)
                ? new BankAccountDestination(account.BankCode ?? string.Empty, reference, account.BranchCode, account.AccountName)
            : rail.Equals("MobileMoney", StringComparison.OrdinalIgnoreCase)
                ? new MobileMoneyDestination(account.MobileNetwork ?? string.Empty, reference, account.AccountName)
            : rail.Equals("Wallet", StringComparison.OrdinalIgnoreCase)
                ? new WalletDestination(reference, account.AccountName)
            : throw new InvalidOperationException($"Unsupported payout destination type '{account.DestinationType}'.");

        var metadata = new Dictionary<string, string>
        {
            ["tenantId"] = tenantId.ToString(),
            ["orderId"] = orderId.ToString(),
            ["orderItemId"] = orderItemId.ToString(),
            ["payoutId"] = payoutId.ToString(),
            ["quoteId"] = quote.Id.ToString()
        };

        return new PayoutInstruction(
            clientReference,
            new Money(quote.DestinationAmount, quote.DestinationCurrency),
            quote.OriginCurrency,
            destination,
            request.Narration ?? DefaultNarration,
            CallbackUrl: null,
            metadata);
    }

    private IReadOnlyCollection<RemittanceDestinationMethod> ResolveSupportedDestinationMethods(
        string destinationCountry,
        string destinationCurrency)
    {
        var methods = new List<RemittanceDestinationMethod>();
        foreach (var method in CandidateDestinationMethods)
        {
            var query = new PartnerConnectorQuery(
                PartnerServiceCategory.Payout, destinationCountry, destinationCurrency, method);
            if (_connectorResolver.TryResolvePayoutConnector(query, out _))
            {
                methods.Add(new RemittanceDestinationMethod(method, destinationCountry, destinationCurrency, null));
            }
        }

        return methods;
    }

    private static bool IsSupportedDestinationRail(string? destinationType)
        => !string.IsNullOrWhiteSpace(destinationType)
            && CandidateDestinationMethods.Any(
                rail => string.Equals(rail, destinationType.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Resolves the payout a partner callback refers to. <c>ClientReference</c> (our globally-unique
    /// <c>REM-{orderId:N}</c>) is authoritative; <c>ProviderReference</c> is provider-scoped and only a
    /// fallback, and on a cross-provider collision the candidate sent via this webhook's provider wins.
    /// </summary>
    private async Task<Payout?> LocateRemittancePayoutAsync(
        string providerCode, PartnerReference reference, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(reference.ClientReference))
        {
            var byClient = await _db.Payouts
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.ClientReference == reference.ClientReference, cancellationToken);
            if (byClient is not null)
            {
                return byClient;
            }
        }

        if (string.IsNullOrEmpty(reference.ProviderReference))
        {
            return null;
        }

        var candidates = await _db.Payouts
            .IgnoreQueryFilters()
            .Where(p => p.ProviderReference == reference.ProviderReference)
            .ToListAsync(cancellationToken);

        if (candidates.Count <= 1)
        {
            return candidates.FirstOrDefault();
        }

        foreach (var candidate in candidates)
        {
            if (await PayoutMatchesProviderAsync(candidate, providerCode, cancellationToken))
            {
                return candidate;
            }
        }

        return null;
    }

    private async Task<bool> PayoutMatchesProviderAsync(
        Payout payout, string providerCode, CancellationToken cancellationToken)
    {
        if (payout.OrderItemId is null)
        {
            return false;
        }

        var item = await _db.OrderItems
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.Id == payout.OrderItemId, cancellationToken);
        var details = TryDeserializeDetails(item?.DetailsJson);
        return details is not null
            && string.Equals(details.ProviderCode, providerCode, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<RemittanceOrderResponse> BuildResponseAsync(Order order, CancellationToken cancellationToken)
    {
        var item = order.Items.OrderBy(i => i.ItemIndex).FirstOrDefault();
        var details = TryDeserializeDetails(item?.DetailsJson);

        Payout? payout = null;
        if (item is not null)
        {
            payout = await _db.Payouts
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.OrderItemId == item.Id, cancellationToken);
        }

        Transmission? transmission = null;
        if (payout is not null)
        {
            transmission = await _db.Transmissions
                .AsNoTracking()
                .Where(t => t.PayoutId == payout.Id)
                .OrderByDescending(t => t.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return new RemittanceOrderResponse(
            order.Id,
            ResolveOrderNumber(order),
            order.Status,
            order.PayerPartyId ?? details?.CustomerPartyId ?? Guid.Empty,
            details?.BeneficiaryPartyId ?? item?.ReceiverPartyId,
            details?.DestinationExternalAccountId ?? payout?.DestinationExternalAccountId ?? Guid.Empty,
            details?.DestinationType ?? payout?.DestinationType ?? string.Empty,
            details?.MaskedAccountIdentifier ?? string.Empty,
            order.OriginCountry ?? string.Empty,
            order.DestinationCountry ?? string.Empty,
            order.CurrencyIn,
            order.CurrencyOut ?? string.Empty,
            order.AmountIn,
            order.AmountOut ?? 0m,
            item?.FeesTotal ?? details?.FeesTotal ?? 0m,
            details?.TotalAmount ?? (order.AmountIn + (item?.FeesTotal ?? 0m)),
            details?.ExchangeRate ?? 0m,
            item?.PricingQuoteId ?? details?.PricingQuoteId ?? Guid.Empty,
            payout?.Id,
            details?.ProviderCode,
            payout?.ClientReference,
            string.IsNullOrEmpty(payout?.ProviderReference) ? null : payout!.ProviderReference,
            transmission?.Status,
            order.CreatedAt,
            transmission?.CreatedAt ?? payout?.CreatedAt,
            order.Status == OrderStatuses.Complete ? order.UpdatedAt ?? order.CreatedAt : null);
    }

    private string ResolveOrderNumber(Order order)
    {
        var entry = _db.Entry(order);
        if (entry.Metadata.FindProperty("OrderNumber") is not null)
        {
            if (entry.Property("OrderNumber").CurrentValue is string number && !string.IsNullOrWhiteSpace(number))
            {
                return number;
            }
        }

        return $"REM-{order.Id.ToString("N")[..12].ToUpperInvariant()}";
    }

    private async Task StoreUntranslatableEventAsync(
        string providerCode, string payloadHash, string body, CancellationToken cancellationToken)
    {
        var exists = await _db.PartnerWebhookEvents
            .IgnoreQueryFilters()
            .AnyAsync(e => e.ProviderCode == providerCode && e.PayloadHash == payloadHash, cancellationToken);
        if (exists)
        {
            return;
        }

        _db.PartnerWebhookEvents.Add(new PartnerWebhookEventRow
        {
            Id = Guid.NewGuid(),
            ProviderCode = providerCode,
            Category = string.Empty,
            EventType = "unknown",
            ProviderReference = string.Empty,
            ClientReference = string.Empty,
            PayloadHash = payloadHash,
            RawPayload = string.Empty,
            SignatureValid = false,
            ReceivedAt = _clock.UtcNow,
            ProcessingStatus = "Failed",
            Error = $"No webhook translator for provider '{providerCode}'."
        });
        await _db.SaveChangesAsync(cancellationToken);
    }

    private string? ResolveWebhookSigningSecret(string providerCode)
        => _configuration[$"Finance:Partners:Webhooks:{providerCode}:SigningSecret"]
            ?? _configuration["Finance:Partners:Webhooks:SigningSecret"];

    private async Task EnsurePartyInTenantAsync(Guid partyId, CancellationToken cancellationToken)
    {
        if (partyId == Guid.Empty)
        {
            throw new ArgumentException("A customer party id is required.", nameof(partyId));
        }

        var exists = await _db.Parties.AsNoTracking().AnyAsync(p => p.Id == partyId, cancellationToken);
        if (!exists)
        {
            throw new InvalidOperationException("Customer party not found in the current tenant.");
        }
    }

    private static void ValidateQuoteRequest(RemittanceQuoteRequest request)
    {
        if (request.OriginAmount.HasValue == request.DestinationAmount.HasValue)
        {
            throw new ArgumentException("Exactly one of originAmount or destinationAmount must be provided.");
        }

        if (request.OriginAmount is <= 0 || request.DestinationAmount is <= 0)
        {
            throw new ArgumentException("Amount must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(request.OriginCurrency) || request.OriginCurrency.Trim().Length != 3
            || string.IsNullOrWhiteSpace(request.DestinationCurrency) || request.DestinationCurrency.Trim().Length != 3)
        {
            throw new ArgumentException("Origin and destination currencies must be 3-letter ISO codes.");
        }

        if (string.IsNullOrWhiteSpace(request.OriginCountry) || request.OriginCountry.Trim().Length != 2
            || string.IsNullOrWhiteSpace(request.DestinationCountry) || request.DestinationCountry.Trim().Length != 2)
        {
            throw new ArgumentException("Origin and destination countries must be 2-letter ISO codes.");
        }
    }

    private static RemittanceOrderDetails? TryDeserializeDetails(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "{}")
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<RemittanceOrderDetails>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string ComputePayloadHash(string body)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(body ?? string.Empty));
        return Convert.ToHexString(bytes);
    }

    private static string Redact(RawProviderResponse raw)
        => JsonSerializer.Serialize(new { raw.Code, raw.Message });

    private static string Normalize(string value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();
}
