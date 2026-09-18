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
using Aonik.Finance.Services.Partners.Connectors.Registry;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Settings;
using Aonik.SharedKernel.Modules;
using Aonik.SharedKernel.Persistence;
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
    private readonly Services.Partners.Connectors.IPartnerConnectorFactory _connectorFactory;
    private readonly Services.Partners.Connectors.Credentials.ICredentialBundleService _bundleService;
    private readonly LedgerPostingService _ledgerPostingService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IConfiguration _configuration;
    private readonly ISettingProvider _settingProvider;
    private readonly IClock _clock;
    private readonly IModuleGate _moduleGate;
    private readonly ILogger<RemittanceOrderService> _logger;

    public RemittanceOrderService(
        FinanceDbContext db,
        IPricingService pricingService,
        IPartnerConnectorResolver connectorResolver,
        Services.Partners.Connectors.IPartnerConnectorFactory connectorFactory,
        Services.Partners.Connectors.Credentials.ICredentialBundleService bundleService,
        LedgerPostingService ledgerPostingService,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider,
        IConfiguration configuration,
        ISettingProvider settingProvider,
        IClock clock,
        IModuleGate moduleGate,
        ILogger<RemittanceOrderService> logger)
    {
        _db = db;
        _pricingService = pricingService;
        _connectorResolver = connectorResolver;
        _connectorFactory = connectorFactory;
        _bundleService = bundleService;
        _ledgerPostingService = ledgerPostingService;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
        _configuration = configuration;
        _settingProvider = settingProvider;
        _clock = clock;
        _moduleGate = moduleGate;
        _logger = logger;
    }

    // ── Quote ────────────────────────────────────────────────────────────────
    public async Task<RemittanceQuoteResponse> QuoteAsync(
        RemittanceQuoteRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateQuoteRequest(request);

        var tenantId = _tenantProvider.GetCurrentTenantId();
        await EnsureCallerOwnsPartyAsync(request.CustomerPartyId, cancellationToken);

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

        // Authorization first — before any idempotency replay — so a different user who guesses the key
        // and a victim's party id can neither confirm nor read another customer's remittance.
        await EnsureCallerOwnsPartyAsync(request.CustomerPartyId, cancellationToken);

        // Idempotency: replaying the same key resumes the existing order. Scope by the caller's customer
        // party (already verified above), NOT just the tenant-wide key — the unique idempotency index
        // spans customers, so without this a user could reuse/guess another customer's key (while passing
        // a party they own) and receive or drive that customer's remittance.
        var existing = await _db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(
                o => o.OrderType == RemittanceOrderType
                    && o.IdempotencyKey == key
                    && o.PayerPartyId == request.CustomerPartyId,
                cancellationToken);

        if (existing is not null)
        {
            // Resume rather than blindly return: a prior attempt may have crashed/failed between any two
            // commits (order saved but debit not posted, dispatched but not settled, …). Driving the order
            // forward is idempotent, so a replay completes a stranded send instead of returning it half-done.
            activity?.SetTag(FinanceActivitySource.OrderIdTag, existing.Id);
            var resumed = await DriveRemittanceAsync(existing, cancellationToken);
            activity?.SetTag(FinanceActivitySource.OutcomeTag,
                existing.Status == OrderStatuses.Failed ? MoneyActionOutcomes.Failed : MoneyActionOutcomes.Success);
            return resumed;
        }

        // Load + validate the locked inputs.
        var quote = await _db.PricingQuotes
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == request.PricingQuoteId, cancellationToken)
            ?? throw new NotFoundException("Pricing quote not found.");

        if (!string.Equals(quote.QuoteType, RemittanceQuoteType, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidStateException("Pricing quote is not a remittance quote.");
        }

        if (quote.ExpiresAt <= _clock.UtcNow)
        {
            _logger.OrderRejected(Guid.Empty, tenantId, "Remittance quote expired.");
            throw new InvalidStateException("Remittance quote has expired.");
        }

        if (quote.CustomerId.HasValue && quote.CustomerId.Value != request.CustomerPartyId)
        {
            throw new InvalidStateException("Pricing quote does not belong to the requested customer.");
        }

        var account = await _db.ExternalPayoutAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == request.DestinationExternalAccountId, cancellationToken)
            ?? throw new NotFoundException("Destination payout account not found.");

        if (account.CustomerPartyId != request.CustomerPartyId)
        {
            throw new InvalidStateException("Destination payout account is not owned by the customer.");
        }

        if (!string.IsNullOrWhiteSpace(account.Currency)
            && !string.Equals(account.Currency, quote.DestinationCurrency, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidStateException("Destination payout account currency does not match the quote.");
        }

        // Reject unsupported rails up front — before any order/debit — rather than silently transmitting
        // them on the wrong rail. Saved destination types are free-form trimmed text, so this (and the
        // instruction builder) compare case-insensitively.
        if (!IsSupportedDestinationRail(account.DestinationType))
        {
            throw new InvalidStateException(
                $"Unsupported payout destination type '{account.DestinationType}'.");
        }

        // Never move money to a rail that has not passed name-enquiry/account resolution. Newly saved
        // beneficiaries default to unverified, so this is the gate that keeps remittance off unverified
        // rails (verification flips IsVerified once the destination is confirmed with the partner).
        if (!account.IsVerified)
        {
            throw new InvalidStateException(
                "Destination payout account is not verified; verify the beneficiary before sending.");
        }

        // Resolve the payout route now and lock the provider code into order details. A verified
        // destination that carries a provider code must dispatch through that same provider; otherwise
        // fall back to the request/provider routing rules from Spec 036.
        var (_, providerCode) = ResolvePayoutConnector(
            ResolveRequestedProviderCode(request.ProviderCode, account.ProviderCode), quote, account.DestinationType);
        // Bind the payout to a persisted Connector row (Spec 042 §9): the verified beneficiary pins its own
        // connector, so a payout to it dispatches through that exact account; otherwise the migrated legacy
        // default connector applies. Guid.Empty still marks the Simulated fallback (no row).
        var connectorRow = await ResolvePayoutConnectorRowAsync(account, providerCode, cancellationToken);
        var connectorId = connectorRow?.Id ?? Guid.Empty;

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

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Lost the idempotency race: a concurrent confirm with the same key passed the initial lookup
            // too, then committed first and tripped the unique (TenantId, OrderType, IdempotencyKey)
            // index here. Detach our rejected graph and return the winner — the debit and connector call
            // are never run for this request, honouring the idempotent-replay contract instead of 500ing.
            DetachConfirmGraph(order, payout, fulfilmentRef);

            var winner = await _db.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(
                    o => o.OrderType == RemittanceOrderType && o.IdempotencyKey == key, cancellationToken);

            if (winner is null)
            {
                throw;
            }

            if (winner.PayerPartyId != request.CustomerPartyId)
            {
                // The tenant-wide idempotency key is already held by a different customer — never return
                // or drive their order.
                throw new InvalidStateException("Idempotency key is already in use.");
            }

            activity?.SetTag(FinanceActivitySource.OrderIdTag, winner.Id);
            activity?.SetTag(FinanceActivitySource.OutcomeTag, MoneyActionOutcomes.SkippedIdempotent);
            return await BuildResponseAsync(winner, cancellationToken);
        }

        _logger.OrderConfirmed(orderId, tenantId, RemittanceOrderType, quote.Id);

        // Drive the freshly-created order to completion (post debit → dispatch → settle). The same driver
        // resumes a replayed order, so the first attempt and a retry share one idempotent path and the
        // order/payout/debit pre-connector state is never left committed without the rest of the flow.
        var response = await DriveRemittanceAsync(order, cancellationToken);
        activity?.SetTag(FinanceActivitySource.OutcomeTag,
            order.Status == OrderStatuses.Failed ? MoneyActionOutcomes.Failed : MoneyActionOutcomes.Success);
        return response;
    }

    // ── Get ──────────────────────────────────────────────────────────────────
    public async Task<RemittanceOrderResponse?> GetAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        // Scoped to the caller's own customer parties (Spec 036 §10.3): an order the user does not own
        // is indistinguishable from a missing one (the endpoint returns 404 either way), so remittance
        // ids cannot be probed across customers within a tenant.
        var userId = _currentUserProvider.GetCurrentUserId();
        if (userId is null || userId.Value == Guid.Empty)
        {
            return null;
        }

        var partyIds = await _db.UserParties
            .AsNoTracking()
            .Where(up => up.UserId == userId.Value)
            .Select(up => up.PartyId)
            .ToListAsync(cancellationToken);

        if (partyIds.Count == 0)
        {
            return null;
        }

        var order = await _db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(
                o => o.Id == orderId
                    && o.OrderType == RemittanceOrderType
                    && o.PayerPartyId != null
                    && partyIds.Contains(o.PayerPartyId.Value),
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

        // Parse the payload to read its reference — UNTRUSTED until the signature verifies (Spec 042 §9.2).
        var translated = translator.Translate(envelope);

        // Use the reference ONLY to locate a candidate payout → its owning connector → that connector's
        // bundle signing secret. ClientReference (REM-{orderId:N}) is our own globally-unique, provider-agnostic
        // key — the authoritative match; ProviderReference is a provider-scoped fallback. Nothing here is
        // trusted for a state change until the signature validates against that secret.
        var payout = await LocateRemittancePayoutAsync(providerCode, translated.Reference, cancellationToken);

        // The owning tenant is known only now, from the payout the callback references — this request is
        // anonymous, so the HTTP module gate had no tenant to check and let it through. Re-check here, before
        // anything is written: a tenant with Finance off gets 403 module.disabled and no inbox row (Spec 097
        // §11). Nothing has been trusted yet, so nothing has been mutated.
        if (payout is not null)
        {
            await _moduleGate.EnsureEnabledAsync(payout.TenantId, ModuleIds.Finance, cancellationToken);
        }

        var candidateConnectorId = payout?.ConnectorId ?? Guid.Empty;
        var candidateConnector = candidateConnectorId != Guid.Empty
            ? await _db.Connectors.AcrossTenants()
                .FirstOrDefaultAsync(c => c.Id == candidateConnectorId, cancellationToken)
            : null;

        var signingSecrets = await ResolveWebhookSigningSecretsAsync(candidateConnector, providerCode, cancellationToken);
        var signatureValid = signingSecrets.Any(secret => translator.VerifySignature(envelope, secret));

        // ConnectorId is stamped only once the signature validates (resolved + trusted, Spec 042 §9.2); an
        // unresolved or rejected event stays in the provider-code dedupe bucket.
        Guid? resolvedConnectorId = signatureValid && candidateConnectorId != Guid.Empty ? candidateConnectorId : null;

        // Connector-aware dedupe: keyed by ConnectorId once resolved, else by ProviderCode.
        var existingEvent = resolvedConnectorId is { } resolved
            ? await _db.PartnerWebhookEvents.AcrossTenants()
                .FirstOrDefaultAsync(e => e.ConnectorId == resolved && e.PayloadHash == payloadHash, cancellationToken)
            : await _db.PartnerWebhookEvents.AcrossTenants()
                .FirstOrDefaultAsync(
                    e => e.ConnectorId == null && e.ProviderCode == providerCode && e.PayloadHash == payloadHash,
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
            ConnectorId = resolvedConnectorId,
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
        else
        {
            inboxRow.SignatureValid = signatureValid;
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
            : await _db.OrderItems.AcrossTenants().FirstOrDefaultAsync(i => i.Id == payout.OrderItemId, cancellationToken);
        var order = item is null
            ? null
            : await _db.Orders.AcrossTenants().FirstOrDefaultAsync(o => o.Id == item.OrderId, cancellationToken);

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

    /// <summary>
    /// Drives a remittance order to completion from whatever committed state it is in — used by both the
    /// first confirm and an idempotent replay. Every step is idempotent (the debit, settlement and reversal
    /// dedupe on their ledger source key; a recorded transmission means the connector was already called),
    /// so a confirm that crashed between any two commits is completed by the next replay rather than left
    /// stranded. Money invariants: the debit posts before any connector call, and the order is marked
    /// terminal only after its settlement/reversal ledger entry is committed.
    /// </summary>
    private async Task<RemittanceOrderResponse> DriveRemittanceAsync(Order order, CancellationToken cancellationToken)
    {
        if (OrderStatuses.IsTerminal(order.Status))
        {
            return await BuildResponseAsync(order, cancellationToken);
        }

        var item = order.Items.OrderBy(i => i.ItemIndex).FirstOrDefault()
            ?? throw new InvalidOperationException("Remittance order has no line item.");
        var details = TryDeserializeDetails(item.DetailsJson)
            ?? throw new InvalidOperationException("Remittance order is missing its locked details.");
        var payout = await _db.Payouts.FirstOrDefaultAsync(p => p.OrderItemId == item.Id, cancellationToken)
            ?? throw new InvalidOperationException("Remittance order has no payout to fulfil it.");

        using var orderScope = _logger.BeginOrderScope(order.Id);

        // 1. Customer debit — before any connector call, idempotent on (tenant, RemittanceDebit, orderId).
        await _ledgerPostingService.PostRemittanceDebitAsync(
            order.TenantId, order.Id, details.TotalAmount, details.OriginCurrency, cancellationToken);

        // 2. A recorded transmission is durable proof the connector was already called: reconcile the
        //    ledger from its result instead of re-dispatching. Otherwise dispatch now.
        var transmission = await _db.Transmissions
            .Where(t => t.PayoutId == payout.Id)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (transmission is null)
        {
            await DispatchRemittanceAsync(order, item, payout, details, cancellationToken);
        }
        else
        {
            await ApplyTerminalResultAsync(order, item, payout, details, ParseStatus(transmission.Status), cancellationToken);
        }

        return await BuildResponseAsync(order, cancellationToken);
    }

    /// <summary>
    /// Resolves the persisted <see cref="Connector"/> row a payout should bind to (Spec 042 §9). A verified
    /// beneficiary pins its own connector (so the payout dispatches through that exact account); otherwise the
    /// migrated legacy-default payout connector applies. Returns null for the Simulated fallback (no row).
    /// </summary>
    private async Task<Connector?> ResolvePayoutConnectorRowAsync(
        ExternalPayoutAccount account, string providerCode, CancellationToken cancellationToken)
    {
        if (account.ConnectorId is { } pinned && pinned != Guid.Empty)
        {
            // A beneficiary verified against a specific connector MUST dispatch through that exact account; if
            // the pinned connector no longer resolves (deleted/disabled) fail closed rather than silently
            // re-routing money through the tenant default (Spec 042 §9).
            return await _db.Connectors.FirstOrDefaultAsync(c => c.Id == pinned, cancellationToken)
                ?? throw new InvalidStateException(
                    $"Beneficiary is bound to connector {pinned}, which no longer exists; "
                    + "re-verify the beneficiary against an active connector before sending.");
        }

        if (string.Equals(providerCode, SimulatedProviderCode, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return await FindDefaultPayoutConnectorRowAsync(cancellationToken);
    }

    /// <summary>The migrated legacy-default payout connector row for this tenant, if any (Spec 042 §7.2).</summary>
    private async Task<Connector?> FindDefaultPayoutConnectorRowAsync(CancellationToken cancellationToken)
    {
        var payoutTypes = ConnectorRegistry.All
            .Where(k => k.Port == PartnerServiceCategory.Payout)
            .SelectMany(k => new[] { k.Kind, k.ProviderCode })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return await _db.Connectors
            .Where(c => c.IsLegacyDefault && payoutTypes.Contains(c.ConnectorType))
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Builds the runtime payout connector for a dispatch: bound to the resolved row when one is set
    /// (account-specific credentials), else the legacy/simulated DI connector resolved by provider code.
    /// </summary>
    private async Task<IPartnerPayoutConnector> ResolveBoundPayoutConnectorAsync(
        Guid connectorId, string providerCode, CancellationToken cancellationToken)
    {
        if (connectorId != Guid.Empty)
        {
            // A payout bound to a connector at confirm time MUST dispatch through that same row; if it no
            // longer resolves, fail closed rather than fall back to the unbound/legacy connector (Spec 042 §9).
            var row = await _db.Connectors.FirstOrDefaultAsync(c => c.Id == connectorId, cancellationToken)
                ?? throw new InvalidStateException(
                    $"Payout is bound to connector {connectorId}, which no longer exists; cannot dispatch.");
            return _connectorFactory.CreatePayout(row);
        }

        return _connectorResolver.ResolvePayoutConnector(providerCode);
    }

    private async Task DispatchRemittanceAsync(
        Order order, OrderItem item, Payout payout, RemittanceOrderDetails details, CancellationToken cancellationToken)
    {
        var account = await _db.ExternalPayoutAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == details.DestinationExternalAccountId, cancellationToken)
            ?? throw new NotFoundException("Destination payout account no longer exists; cannot dispatch.");

        var connector = await ResolveBoundPayoutConnectorAsync(payout.ConnectorId, details.ProviderCode, cancellationToken);
        var instruction = BuildPayoutInstruction(
            payout.ClientReference, details, account, order.TenantId, order.Id, item.Id, payout.Id);

        PartnerTransactionStatus status;
        PartnerReference? reference = null;
        RawProviderResponse? raw = null;
        string? failure = null;

        try
        {
            var result = await connector.InitiatePayoutAsync(instruction, cancellationToken);
            status = result.Status;
            reference = result.Reference;
            raw = result.Raw;
        }
        catch (Exception ex)
        {
            status = PartnerTransactionStatus.Failed;
            failure = ex.Message;
            _logger.PaymentTransmitFailed(order.Id, order.TenantId, details.ProviderCode, ex.Message, ex);
        }

        // Record the transmission FIRST so a resumed confirm never re-dispatches; the connector is also
        // idempotent on ClientReference (the key we send the partner) as the cross-process backstop.
        payout.Status = status.ToString();
        payout.ProviderReference = reference?.ProviderReference ?? string.Empty;
        _db.Transmissions.Add(new Transmission
        {
            Id = Guid.NewGuid(),
            TenantId = order.TenantId,
            PayoutId = payout.Id,
            ConnectorId = details.ConnectorId,
            IdempotencyKey = payout.ClientReference,
            ProviderReference = reference?.ProviderReference,
            Status = status.ToString(),
            RetryCount = 0,
            LastError = failure,
            RawResponseJson = raw is null ? null : Redact(raw)
        });
        await _db.SaveChangesAsync(cancellationToken);

        await ApplyTerminalResultAsync(order, item, payout, details, status, cancellationToken);
    }

    /// <summary>
    /// Reconciles order/item/payout status and the settlement/reversal ledger entry for a connector result.
    /// The ledger entry is posted (idempotently) BEFORE the order is marked terminal, so an order is never
    /// Complete/Failed without its corresponding ledger entry; a resumed confirm re-runs this safely.
    /// </summary>
    private async Task ApplyTerminalResultAsync(
        Order order, OrderItem item, Payout payout, RemittanceOrderDetails details, PartnerTransactionStatus status,
        CancellationToken cancellationToken)
    {
        var (orderStatus, itemStatus, settle, reverse, terminal) = MapResult(status);
        payout.Status = status.ToString();

        if (settle)
        {
            await _ledgerPostingService.PostRemittanceSettlementAsync(
                order.TenantId, payout.Id, order.Id, details.TotalAmount, details.OriginCurrency, cancellationToken);
        }
        else if (reverse)
        {
            await _ledgerPostingService.PostRemittanceFailureReversalAsync(
                order.TenantId, payout.Id, order.Id, details.TotalAmount, details.OriginCurrency, cancellationToken);
        }

        order.Status = orderStatus;
        item.Status = itemStatus;
        await _db.SaveChangesAsync(cancellationToken);

        if (settle || !terminal)
        {
            _logger.PaymentTransmitted(order.Id, order.TenantId, details.ProviderCode, payout.ProviderReference);
        }
    }

    private static PartnerTransactionStatus ParseStatus(string? value)
        => Enum.TryParse<PartnerTransactionStatus>(value, ignoreCase: true, out var status)
            ? status
            : PartnerTransactionStatus.Unknown;

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

    private static string? ResolveRequestedProviderCode(string? requestProviderCode, string? destinationProviderCode)
    {
        var requested = string.IsNullOrWhiteSpace(requestProviderCode) ? null : requestProviderCode.Trim();
        var destination = string.IsNullOrWhiteSpace(destinationProviderCode) ? null : destinationProviderCode.Trim();

        if (destination is null)
        {
            return requested;
        }

        if (requested is not null && !string.Equals(requested, destination, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidStateException(
                "Requested payout provider does not match the verified destination provider.");
        }

        return destination;
    }

    private static PayoutInstruction BuildPayoutInstruction(
        string clientReference,
        RemittanceOrderDetails details,
        ExternalPayoutAccount account,
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
            ["quoteId"] = details.PricingQuoteId.ToString()
        };

        return new PayoutInstruction(
            clientReference,
            new Money(details.DestinationAmount, details.DestinationCurrency),
            details.OriginCurrency,
            destination,
            string.IsNullOrWhiteSpace(details.Narration) ? DefaultNarration : details.Narration!,
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
                .AcrossTenants()
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
            .AcrossTenants()
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
            .AcrossTenants()
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
            .AcrossTenants()
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

    /// <summary>
    /// The signing secrets a webhook may validate against (Spec 042 §9.2, §11). A <strong>bound</strong>
    /// connector (one carrying a <c>CredentialsRef</c>) verifies <strong>only</strong> against its own bundle's
    /// <em>current</em> signing secret plus, during a rotation window, the <em>previous</em> one — expiry is
    /// enforced at read time via <c>_clock.UtcNow</c>. If that bundle omits the signing secret the candidate
    /// set is empty and the webhook is <strong>rejected</strong>: a bound connector must never borrow the
    /// tenant's global legacy secret, which belongs to a different account (fail-closed, §7.2). The legacy
    /// fallback is reserved for unbound / legacy-default connectors and events not resolved to a connector.
    /// </summary>
    private async Task<IReadOnlyList<string>> ResolveWebhookSigningSecretsAsync(
        Connector? connector, string providerCode, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(connector?.CredentialsRef))
        {
            var bundle = await _bundleService.ResolveAsync(connector.CredentialsRef!, cancellationToken);
            return bundle?.Secrets.GetVerificationCandidates(ConnectorRegistry.FieldSigningSecret, _clock.UtcNow)
                ?? Array.Empty<string>();
        }

        var legacy = await ResolveWebhookSigningSecretAsync(providerCode, cancellationToken);
        return string.IsNullOrEmpty(legacy) ? Array.Empty<string>() : new[] { legacy };
    }

    private async Task<string?> ResolveWebhookSigningSecretAsync(
        string providerCode,
        CancellationToken cancellationToken)
    {
        if (string.Equals(providerCode, "Flutterwave", StringComparison.OrdinalIgnoreCase))
        {
            var stored = await _settingProvider.GetAsync(
                PartnerGatewaySettingNames.FlutterwaveSigningSecret,
                cancellationToken);
            if (!string.IsNullOrWhiteSpace(stored))
            {
                return stored;
            }
        }

        return _configuration[$"Finance:Partners:Webhooks:{providerCode}:SigningSecret"]
               ?? _configuration["Finance:Partners:Webhooks:SigningSecret"];
    }

    private async Task EnsureCallerOwnsPartyAsync(Guid partyId, CancellationToken cancellationToken)
    {
        if (partyId == Guid.Empty)
        {
            throw new ArgumentException("A customer party id is required.", nameof(partyId));
        }

        var userId = _currentUserProvider.GetCurrentUserId();
        if (userId is null || userId.Value == Guid.Empty)
        {
            throw new UnauthorizedAccessException("No authenticated user to authorize this remittance.");
        }

        // The requested customer party MUST be one the authenticated user is linked to via the UserParty
        // bridge — never trust an arbitrary party id from the request body (Spec 036 §11). Proving the
        // destination account belongs to the supplied party is not enough; the supplied party must be the
        // caller's. Fails closed (tenant-scoped by the UserParty query filter).
        var owns = await _db.UserParties
            .AsNoTracking()
            .AnyAsync(up => up.UserId == userId.Value && up.PartyId == partyId, cancellationToken);

        if (!owns)
        {
            throw new UnauthorizedAccessException(
                "The requested customer party does not belong to the authenticated user.");
        }
    }

    private void DetachConfirmGraph(Order order, Payout payout, OrderFulfilmentRef fulfilmentRef)
    {
        // The order graph was cascade-tracked as Added by _db.Orders.Add; detach every node plus the
        // payout and fulfilment ref so the rejected confirm can't be replayed by a later SaveChanges.
        foreach (var historyEvent in order.HistoryEvents)
        {
            _db.Entry(historyEvent).State = EntityState.Detached;
        }

        foreach (var partyRole in order.PartyRoles)
        {
            _db.Entry(partyRole).State = EntityState.Detached;
        }

        foreach (var item in order.Items)
        {
            _db.Entry(item).State = EntityState.Detached;
        }

        _db.Entry(order).State = EntityState.Detached;
        _db.Entry(payout).State = EntityState.Detached;
        _db.Entry(fulfilmentRef).State = EntityState.Detached;
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
