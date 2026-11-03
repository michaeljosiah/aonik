# Remittance transfer editor

The Admin UI provides a dedicated human-operated transfer flow at `/orders/transfers/new`, with read-only execution details at `/orders/transfers/:orderId`. The Remittance profile exposes **New transfer** beside **Transfer orders**. The general profile includes the supporting routes; other business profiles retain their existing navigation.

The editor uses the application's shadcn components, semantic light/dark tokens and shared SVG menu icons. Operators select an existing customer and a saved payout destination, choose the corridor and send/receive amount basis, enter a purpose code, and request a server-priced quote. Only verified destinations supported by the quote can be confirmed. Saved account identifiers remain masked; the separate [Beneficiaries screen](remittance-beneficiaries.md) creates and verifies destinations. The editor links to it with the selected customer and can refresh its destination list. Its **Send transfer** actions return to the editor with the customer and verified destination selected, using a reloadable link containing IDs only. Both selections are reloaded from the current tenant’s APIs; pending-confirmation recovery takes precedence.

The review shows the send amount, received amount, fees, total debit, FX rate, fee breakdown and quote expiry. Any input change invalidates the displayed quote. An expired quote cannot start a new confirmation. A review checkbox and confirmation dialog identify the exact debit, destination and purpose before the operator authorises execution.

## Authorization and execution

Operator endpoints are separate from the customer-owned `/payabo/remittance/*` endpoints. Customer ownership checks remain in place on those existing APIs; staff do not acquire artificial customer links.

| Endpoint | Policy | Permissions |
| --- | --- | --- |
| `POST /remittance/quote` | `AdminWritePolicy` | `Payment.Create` |
| `GET /remittance/customers/{customerPartyId}/destinations` | `AdminReadPolicy` | `Payment.Read` |
| `POST /remittance/confirm` | `AdminWritePolicy` | `Payment.Create`, `Payment.Capture` |
| `GET /remittance/{id}` | `AdminReadPolicy` | `Payment.Read` |
| `GET /remittance/{id}/receipt` | `AdminReadPolicy` | `Payment.Read` |
| `GET /remittance/{id}/receipt/email` | `AdminWritePolicy` | `Payment.Read`, `Customers.Read`, `Customers.Write` |
| `POST /remittance/{id}/receipt/email` | `AdminWritePolicy` | `Payment.Read`, `Customers.Read`, `Customers.Write` |
| `GET /remittance/{id}/receipt/email/{requestId}` | `AdminWritePolicy` | `Payment.Read`, `Customers.Read`, `Customers.Write` |
| `GET /remittance/{id}/receipt/email/history` | `AdminReadPolicy` | `Payment.Read`, `Customers.Read` |

The editor route additionally requires `Customers.Read`, `Payment.Read` and `Payment.Create`. Users without capture permission can prepare a quote but cannot confirm it. Read-only staff can inspect transfer details. Profiles, enabled modules and policies continue to constrain route/menu visibility; the server independently enforces access and tenant boundaries.

Confirmation requires `confirmExecution: true` and an `Idempotency-Key`. It uses the existing remittance execution service: lock the quote, create the intent order and payout, post the ledger debit, and dispatch through the configured connector. This is a financially material operator action. Orders remain distinct from payment/payout execution and ledger evidence. No new AI tool or agent bypasses the proposal/approval boundary, and no schema migration is needed.

## Recovery

Before sending, the browser records the exact request and key in `sessionStorage`, scoped to tenant and signed-in user. It stores pricing, references, the masked destination and the operator note, rather than raw account details. Requests retain the tenant selected when the action began.

If the response is lost or the result is uncertain, inputs remain locked. Reloading the same tab recovers that request. **Retry same transfer** reuses the original body/key so the existing server driver can resume or return the original order, including after quote expiry. An existing key cannot be rebound to different transfer details. The editor never automatically retries execution or starts a replacement transfer.

The server can explicitly reject a confirmation before execution with `X-Transfer-Execution: not-started`, after verifying that no order exists for its key. Only that verdict clears an unsuccessful attempt and permits a new quote. Successful confirmation clears the journal and opens read-only details, where operators can refresh status and inspect order, quote, payout, provider and transmission references. A pending request that cannot be recovered requires checking transfer activity through the existing operational workflow; the UI does not silently discard it. Recovery is tab-local, so closing the tab or clearing browser storage is not a durable transfer-history mechanism.

Live operation requires configured pricing/FX, corridors, compliant customers, verified payout beneficiaries, ledger funding and working partner connectors. These are existing services and operational data, not seeded by the business pack.

## Verification and screenshots

Service tests cover operator permissions, tenant isolation, customer-ownership preservation, explicit confirmation, pre-execution rejection and idempotent replay without duplicate order/payout/transmission. API integration tests cover staff audience, missing capture permission, validation and read access. UI tests cover amount validation, quote expiry, supported destinations, transport context, recovery data and route filtering.

For repeatable screenshots and browser interaction checks, start the Admin UI in mock authentication mode on port 4175, then run from the repository root:

```powershell
$env:VITE_AUTH_PROVIDER='mock'
# In src/Aonik.AdminUi:
npm run dev -- --host 127.0.0.1 --port 4175
# In another terminal at the repository root:
$env:WRITE_RECEIPT_EXAMPLES='1'
dotnet test tests/Aonik.Infrastructure.Tests --filter FullyQualifiedName~RemittanceReceiptRendererTests
node src/Aonik.AdminUi/scripts/verify-transfer-editor.mjs
```

The browser script intercepts API requests with fictional responses. It verifies customer selection, unavailable destinations, quote invalidation, explicit confirmation, rejection recovery, lost-response/reload recovery, identical retry body/key/tenant and read-only details. It captures the actual production React UI in light and dark under `Templates/aonik-admin-starterkit/marketing/screenshots/` and writes `transfer-editor-report.json`. It does not execute a live transfer.

## Downloadable transfer receipt

The details page provides **Download receipt** using the shared document glyph and shadcn Button. Each click re-fetches the order through the existing tenant-scoped, `Payment.Read`/`AdminReadPolicy` operator endpoint. An unavailable order or refused request produces an error; it never falls back to exporting the cached screen. Leaving the order/tenant cancels the operation. No transfer execution or financial mutation is called.

The download is an A4 PDF named `transfer-receipt-<order-number>.pdf`. It includes the workspace name/reference, order and payout status, sending amount, fees, total debit, recipient amount, FX rate, corridor, masked destination, UTC timestamps and execution references. It contains IDs already returned by the details API rather than provider beneficiary tokens, raw account numbers or extra party data. The UI refreshes to the downloaded snapshot after success.

A successful order plus successful transmission is labelled Completed. Pending instructions and unsuccessful transfers explicitly say they do not confirm recipient payment. The PDF is a snapshot at download time, not a digitally signed settlement certificate; ledger records remain the source of financial truth. Missing optional timestamps/references are labelled Not recorded. A typical transfer fits on one page, with continuation pages for long references. Values and identifiers wrap rather than being truncated.

The printable design uses white paper, the application's neutral/teal palette, its DM Sans body font and the same transfer SVG as the UI. The canonical PDF is generated on the server by PDFsharp 6.2.4 with embedded, licensed fonts and an application-local generated icon. Email and download share this renderer; neither accepts receipt bytes or amounts from the browser. No fonts are fetched at runtime. Unsupported glyphs produce an explicit error instead of silently changing names or references. No schema migration is needed.

The browser verification script now checks actual PDF downloads for completed and pending snapshots, fresh status retrieval and revoked-access failure. It writes the fictional completed example to `output/pdf/remittance-transfer-receipt-example.pdf`; pending output is under `tmp/pdfs/` for validation. Render the PDF and verify its amounts, status, references and pagination when changing receipt layout or font handling.

## Emailing transfer receipts

**Send receipt** appears beside Download receipt for staff with the required write policy and customer permissions. Its shadcn dialog and shared communication icon support both themes. The dialog loads saved, valid email contacts for the transfer's current-tenant customer, identifies the customer, order, status and attachment, and requires an explicit confirmation. It provides a contact-management link when no valid address exists. It accepts no arbitrary recipient or browser-authored receipt.

The server re-authorizes the transfer, verifies the saved contact still belongs to the customer and matches the address reviewed by the operator, generates the PDF from authoritative order/payout data and sends the seeded `remittance.transfer-receipt` template through the existing tenant-aware email service. HTML values are escaped and published business branding is authoritative. The ACS adapter preserves the PDF attachment. Existing communication provider and sender settings must be configured for live sends.

Before contacting the provider, the service reserves the request UUID as an existing order-history event primary key. A reference is bound to its contact and email hash; repeated or concurrent requests cannot dispatch another message. The append-only request and outcome events record the operator, timestamp, masked recipient, file name and content hash; they do not store raw email addresses or PDF bytes. No financial state changes and no new AI mutation tool are introduced.

The UI stores only request/contact references and an address hash in tab-local recovery storage, scoped to tenant, authenticated user and order. A lost response or reload offers **Check send status** and an explicitly confirmed **Retry same request**, retaining the same reference. Provider acceptance is reported as **submitted for email delivery**, never as inbox delivery. A definitive configuration/rejection failure permits a fresh confirmed attempt. A transport error or interrupted process retains an Unknown/Sending claim and prevents implicit duplicate dispatch; operators must reconcile unresolved provider acceptance through delivery logs before a separate send. Recovery does not imply a durable email queue or automatic background retry.

Unit, API and browser checks cover attachment transport, escaped templates, tenant-scoped contacts, authorization, changed addresses, explicit confirmation, idempotency, pending/failed/unknown outcomes and read-only visibility. Browser fixtures send no real email and capture `remittance-send-receipt-{light,dark}@2x.png` plus `receipt-email-report.json` in the marketing screenshots directory.

## Receipt email history

Provider delivery evidence is described in [Receipt email delivery tracking](receipt-email-delivery.md), including Event Grid setup, delivered/bounced statuses and matching editable artboards.

Transfer details includes **Receipt email history**, using the shared communication/refresh icons, shadcn Card, Table and Badge components, and semantic light/dark styles. Staff with `Payment.Read`, `Customers.Read` and `AdminReadPolicy` can inspect it without acquiring send permission. The history refreshes after a send or send-status check and offers manual refresh and server pagination (20 rows per page; API maximum 50).

Each row shows the UTC request time, masked recipient recorded at send time, PDF filename, stable send reference, recorded status and UTC outcome time. The historical recipient is never reconstructed from the customer's current contacts. Existing audit records retain only a masked address and hash, so full historical email addresses cannot be recovered. Changing or removing a contact does not change an earlier row.

The UI labels provider acceptance **Submitted**, a definitive rejection **Not sent**, an uncertain response **Unconfirmed**, and a request with no recorded outcome **Pending**. The separate **Delivery** column shows recorded provider reports and their UTC timestamps. Refreshing reads this recorded evidence without contacting the provider or sending another email. A pending row has no outcome timestamp. The query joins request, outcome and delivery audit events only for the current tenant and transfer, returns newest requests first with a stable tie-breaker, and never exposes the stored address/PDF hashes. No schema change is required.

The browser fixture verifies automatic refresh, all four statuses, empty/error recovery, paging, tenant context and read-only visibility without sending real email. It captures `remittance-receipt-history-{light,dark}@2x.png` and writes `receipt-history-report.json` alongside the other marketing captures.

## Resending a receipt

History rows with Submitted or Not sent outcomes offer **Resend receipt** to operators who meet the existing send permissions and write policy. Pending and Unconfirmed rows keep this action disabled until their earlier outcome is resolved; read-only viewers see no send actions. The shared communication icon and the existing shadcn confirmation dialog are reused.

Opening a resend identifies the previous masked recipient and send reference, reloads the customer's valid saved email addresses, and requires choosing an address and explicitly confirming it. The UI does not guess a saved contact from a masked historical address. Cancelling sends nothing. The PDF is regenerated from the current saved transfer state, since original attachment bytes are not archived.

A confirmed additional copy receives a fresh request UUID through the existing receipt send endpoint, creating its own audit/history entry. An unresolved tab-local request takes precedence: a resend cannot silently replace it with a new UUID. The operator can check the earlier status or recover the original request through Send receipt. Existing server recipient validation, permissions, tenant scoping and idempotency remain authoritative. No new endpoint or schema migration is needed.

Browser checks cover cancellation, required selection and confirmation, a fresh UUID for another copy, updated history, uncertain-send recovery and read-only visibility. Captures are `remittance-resend-receipt-{light,dark}@2x.png` with `receipt-resend-report.json`. Editable design artboards for these flows and the preceding Remittance work are in `Templates/aonik-admin-starterkit/screens/remittance-operations.jsx`, registered in both the main canvas and `remittance.html`.
