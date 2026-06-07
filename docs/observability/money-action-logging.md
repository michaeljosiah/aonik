# Money-action logging convention

> Reviewer-checkable rule: every money-touching code path MUST emit
> `MoneyActionLog.*` at entry and at outcome, open a
> `FinanceActivitySource` span, and resolve OrderId into a
> `BeginOrderScope` as early as possible.

This document defines the observability contract that backs **GitHub
Issue #142** — *"given an OrderId, all related log entries (quote,
confirm, capture, transmit, settle, webhooks) can be retrieved in under
30 seconds in App Insights / structured logs."*

If a new code path moves money and skips this convention, an operator
triaging a failed order will be flying blind. The saved KQL query, the
admin UI trace endpoint, and the lifecycle dashboard all depend on the
keys and conventions defined here.

## The moving pieces

| Component | File | Role |
| --- | --- | --- |
| `FinanceActivitySource` | `src/Aonik.Finance/Services/Observability/FinanceActivitySource.cs` | OpenTelemetry source for money-action spans. Registered in `Aonik.ServiceDefaults`. Use `Source.StartActivity(...)`; tag with the constants on the same class (`OrderIdTag`, `StageTag`, `OutcomeTag`, etc.). |
| `MoneyActionStages` | (same file) | Closed-set stage values: `quote`, `confirm`, `capture`, `transmit`, `settle`, `webhook`. Don't invent new stages — extend the enum and update the KQL. |
| `MoneyActionOutcomes` | (same file) | Closed-set outcome values: `success`, `failed`, `rejected`, `skipped_idempotent`, `timeout`. Same rule — don't invent. |
| `MoneyActionLog` | `src/Aonik.Finance/Services/Observability/MoneyActionLog.cs` | Source-generated `[LoggerMessage]` events. 16 typed methods, EventId schema 11xx–16xx, all sharing `EventName = "MoneyAction"`. Public wrappers push `Stage` + `Outcome` into `BeginScope` so the saved KQL filter on `customDimensions.Stage == "capture"` works across every capture event regardless of method. |
| `OrderLogScope` | `src/Aonik.Finance/Services/Observability/OrderLogScope.cs` | `BeginOrderScope(orderId, ...)` extension. Opens a `BeginScope` carrying `OrderId` (+ optional `PaymentIntentId` / `InvoiceId`) so every child log inherits the keys. **Broad-scope policy:** keep the scope open for the entire money-path request — EF queries, downstream service calls, exceptions all need to carry `OrderId` for forensics. |
| `GetMoneyActionTraceAsync` | `src/Aonik.Infrastructure/Observability/AppInsightsQueryService.cs` | Runtime equivalent of the saved KQL query. Returns a typed timeline + the query wall-clock for the 30s SLA check. |
| Saved KQL | `docs/observability/queries/money-action-by-orderid.kql` | The same query the runtime API runs, kept in source so a reviewer can read it without booting App Insights. |
| Admin API endpoint | `GET /admin/observability/money-actions/{OrderId}` | Backs the Admin UI trace pane. Wraps `GetMoneyActionTraceAsync`. |

## EventId schema

```
11xx — Quote stage
12xx — Confirm stage
13xx — Capture stage
14xx — Transmit stage
15xx — Settle stage
16xx — Webhook stage

within each stage:
  01 — success
  02 — failed / error
  03 — idempotent skip
  (etc — see MoneyActionLog.cs)
```

Don't reuse an EventId across stages. The runbook walks EventId ranges
when grouping by stage; reuse breaks the grouping.

## The lifecycle, in code

### Quote — `PricingService.GetBillPaymentQuoteAsync`

At quote time **OrderId does not yet exist** — the customer is shopping
for a price. Use `PricingQuoteId` as the correlation key. The
Confirm-stage event carries both ids; the saved KQL chains via that.

```csharp
using var activity = FinanceActivitySource.Source.StartActivity("pricing.quote");
activity?.SetTag(FinanceActivitySource.StageTag, MoneyActionStages.Quote);
activity?.SetTag(FinanceActivitySource.TenantIdTag, tenantId);

try {
    // ... compute the quote ...
    activity?.SetTag("pricing_quote.id", pricingQuoteId);
    activity?.SetTag(FinanceActivitySource.OutcomeTag, MoneyActionOutcomes.Success);
    _logger.QuoteCreated(pricingQuoteId, tenantId, "BillPayment USD->KES", amount, currency);
    return response;
} catch (Exception ex) {
    activity?.SetTag(FinanceActivitySource.OutcomeTag, MoneyActionOutcomes.Failed);
    activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
    _logger.QuoteFailed(capturedQuoteId, tenantId, action, ex.Message, ex);
    throw;
}
```

### Confirm — `OrderService.SubmitOrderAsync`

This is the **join point**. The OrderConfirmed event MUST carry
`PricingQuoteId` so the saved KQL can chain back to Quote-stage logs.

```csharp
using var orderScope = _logger.BeginOrderScope(orderId);
using var activity = FinanceActivitySource.Source.StartActivity("order.confirm");
activity?.SetTag(FinanceActivitySource.StageTag, MoneyActionStages.Confirm);
activity?.SetTag(FinanceActivitySource.OrderIdTag, orderId);

// ... existing body ...

var firstQuoteId = order.Items.Select(i => i.PricingQuoteId).FirstOrDefault(id => id.HasValue);
_logger.OrderConfirmed(order.Id, order.TenantId, $"Status={order.Status}", firstQuoteId);
```

### Capture — `PaymentService.CapturePaymentAsync`

Span opens **before** the permission check so unauthorized attempts are
still traceable. OrderId is resolved from the loaded `PaymentIntent`.
The endpoint's silent `catch (InvalidOperationException) → 404` is fine
because the service already logged the failure before re-throwing.

```csharp
using var activity = FinanceActivitySource.Source.StartActivity("payment.capture");
activity?.SetTag(FinanceActivitySource.StageTag, MoneyActionStages.Capture);
activity?.SetTag(FinanceActivitySource.PaymentIntentIdTag, paymentIntentId);

try {
    await EnsurePermissionAsync(...);
    var paymentIntent = await _dbContext.PaymentIntents.FirstOrDefaultAsync(...);
    // ... resolve OrderId once intent is loaded ...
    using var orderScope = _logger.BeginOrderScope(orderId, paymentIntentId: paymentIntentId);
    // ... existing body ...
    _logger.PaymentCaptured(orderId, tenantId, paymentIntentId, amount, currency);
} catch (Exception ex) {
    _logger.PaymentCaptureFailed(orderId, tenantId, paymentIntentId, ex.Message, ex);
    throw;
}
```

### Transmit — outbound dispatch to a partner connector

The connector interfaces (`IPartnerBillPaymentConnector`,
`IPartnerPayoutConnector`, `IPartnerCollectionConnector`) exist but no
caller currently invokes them. **When the first real call site lands**,
wrap it in `MoneyActionLog.PaymentTransmitted/Failed/Timeout` keyed by
OrderId. The connector itself stays vendor-agnostic; instrumentation
lives at the caller because OrderId lives at the caller.

```csharp
using var activity = FinanceActivitySource.Source.StartActivity("payment.transmit");
activity?.SetTag(FinanceActivitySource.StageTag, MoneyActionStages.Transmit);
activity?.SetTag(FinanceActivitySource.OrderIdTag, orderId);

try {
    var result = await _connector.PayBillAsync(instruction, ct);
    _logger.PaymentTransmitted(orderId, tenantId, _connector.ProviderCode, result.Reference.ProviderReference ?? "");
} catch (TimeoutException) {
    _logger.PaymentTransmitTimeout(orderId, tenantId, _connector.ProviderCode, stopwatch.ElapsedMilliseconds);
    throw;
} catch (Exception ex) {
    _logger.PaymentTransmitFailed(orderId, tenantId, _connector.ProviderCode, ex.Message, ex);
    throw;
}
```

### Settle — `LedgerPostingService.PostBalancedEntryAsync`

OrderId is optional here — invoice-driven settlement may not have one.
The fast-path idempotency skip AND the lost-race recovery path both
emit `LedgerPostSkippedIdempotent` — previously both were silent and
operators had no signal that a duplicate post was suppressed.

### Webhook — payment-processor webhook handler

No payment-processor webhook endpoint exists yet. **When the first
endpoint lands**, the convention is:
1. Verify the signature.
2. Resolve the OrderId from the payload as step one (translator → DB
   lookup via ClientReference, typically).
3. `using var _ = _logger.BeginOrderScope(orderId);`
4. `_logger.WebhookReceived(orderId, tenantId, providerCode, eventKind);`
5. Process the event.
6. `_logger.WebhookProcessed(orderId, tenantId, providerCode, eventKind, outcome);`
7. On signature failure: `_logger.WebhookRejected(...)` and 401/403.

**Plaid account-linking webhooks are out of scope** — they process
ItemId/account events, not OrderId-bearing events.

### Outbox — `OutboxProcessor.ProcessMessageAsync`

The processor probes the message payload for an `OrderId` JSON property
via a tiny envelope record and pushes it into `BeginScope` before
dispatch. New OrderId-bearing integration events get scope enrichment
for free without registering them with the processor.

## What's NOT in scope

- **FxQuoteService.CreateAsync** — operator-driven FX rate snapshot CRUD,
  not a customer pricing quote. Out of scope for Order correlation.
- **PartnerAdminService** — CRUD over partner records, not dispatch.
- **PlaidAccountWebhookProcessor** — account-linking domain, not orders.
- Plain ledger reads/lists — `MoneyActionLog` is only for *write*
  paths that change money state.

## Saved KQL + runtime API

Saved query: `docs/observability/queries/money-action-by-orderid.kql`.
Save it in App Insights under the name **"Money action by OrderId"** so
operators can re-run it from the saved-queries panel.

Runtime equivalent: `IObservabilityService.GetMoneyActionTraceAsync`
(implementation in `AppInsightsQueryService`). Wired to:

```
GET /admin/observability/money-actions/{OrderId}?timeRange=24h
```

The response envelope includes `QueryDurationMs`. Watch it in the Admin
UI — Issue #142 acceptance is **wall-clock under 30 000 ms**. If it
slips above 10 s on dev with realistic data, file a follow-up before it
turns into a real-incident regression.

## Verification (manual E2E in dev)

App Insights ingestion lag is typically 2–5 minutes — out of our
control. The 30 s SLA is **query execution**, not log freshness.
Manual procedure:

1. Deploy the branch to dev.
2. Create an Order end-to-end through the Admin UI or seeded harness
   (Quote → Confirm → Capture path).
3. Note the OrderId.
4. Wait ~3 minutes for App Insights ingestion.
5. Hit `GET /admin/observability/money-actions/{OrderId}` or run the
   saved KQL in App Insights.
6. Assert:
   - All four wired stages present (Quote, Confirm, Capture, Settle).
   - `PricingQuoteId` envelope is populated.
   - `QueryDurationMs` < 30 000.

The structured-logging contract itself is locked down by
`tests/Aonik.Finance.Tests/Observability/MoneyActionLogTests.cs` — if
EventName or any structured-property key drifts there, those tests fail
in CI before the change reaches dev.

## Reviewer checklist

When reviewing a PR that adds a money-touching code path, look for:

- [ ] `using var activity = FinanceActivitySource.Source.StartActivity(...);`
      at the top of the method
- [ ] Span tagged with `StageTag` + `TenantIdTag` (and `OrderIdTag`
      when OrderId is known)
- [ ] `_logger.BeginOrderScope(orderId, ...)` as soon as OrderId is
      resolved
- [ ] `MoneyActionLog.X(...)` at the success branch
- [ ] `MoneyActionLog.X(...)` at every catch branch (with the exception
      passed through)
- [ ] No previously-silent branch (idempotency skip, race-recovery,
      authorization failure) goes unlogged

If any of those are missing, ask for a fix or explicit justification
in the PR thread before merging.
