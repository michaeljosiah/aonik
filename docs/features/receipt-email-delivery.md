# Receipt email delivery tracking

Transfer receipt history displays **send acceptance** and **delivery** separately. A successful send operation means submission to the provider. Delivered means acceptance by the recipient's mail server; it does not confirm reading or a particular inbox folder. Bounced means a hard delivery rejection. These meanings follow the [ACS delivery-event contract](https://learn.microsoft.com/en-us/azure/event-grid/communication-services-email-events).

## Data flow

Before sending a new receipt, the service reserves its request UUID and records that delivery tracking is enabled. The template pipeline passes this UUID to ACS SDK 1.1.0's operation-ID overload. This lets a delivery event correlate even if it arrives before send polling finishes, or after the HTTP response is lost. Ordinary emails without a tracking ID retain their existing transport path. See the [SDK send overload](https://learn.microsoft.com/en-us/dotnet/api/azure.communication.email.emailclient.sendasync?view=azure-dotnet).

The authenticated receiver appends a delivery audit event to the existing transfer history. It does not update the original request or outcome. Raw recipient addresses, SMTP messages and attachment bytes are not stored in delivery events. Provider event IDs are hashed. A deterministic event key makes duplicate delivery notifications idempotent. Event types are correlated to the receipt request (`ReceiptDelivery:<request UUID without hyphens>`) so a history page queries reports only for its displayed requests.

Reports must match a tracked request in the authenticated tenant and its recorded recipient hash. Other application emails sharing the provider resource are ignored. Older receipts without a tracking reference remain **Unavailable**; matching their current customer contact is not sufficient to reconstruct delivery evidence.

The projection uses provider attempt timestamps rather than arrival order. Terminal delivery results take precedence over distribution-list expansion. Among terminal results the newest timestamp wins; timestamp ties resolve deterministically in this order: Bounced, Suppressed, FilteredSpam, Quarantined, Failed, Delivered. A valid report also establishes that a previously uncertain send reached the provider, so status recovery can clear that request without resending it. The original uncertain outcome remains in the audit trail.

## Provider subscription setup

Live updates require an Azure Event Grid subscription. This implementation does not create one or send a test email automatically. Configure each tenant's expected subscription through deployment configuration or the secret store:

```json
{
  "ReceiptEmailDelivery": {
    "Subscriptions": [
      {
        "TenantId": "<tenant UUID>",
        "Topic": "/subscriptions/<subscription>/resourceGroups/<group>/providers/Microsoft.Communication/communicationServices/<resource>",
        "SubscriptionName": "receipt-delivery",
        "SenderAddress": "receipts@example.com",
        "SharedSecret": "<random secret of at least 32 characters, from your secret store>"
      }
    ]
  }
}
```

The corresponding environment-variable prefix is `ReceiptEmailDelivery__Subscriptions__0__`. Do not commit a live secret. Empty subscriptions or missing/short secrets deny all notifications.

Create an Event Grid subscription on that exact ACS resource with:

- Endpoint: `https://<api-host>/integrations/email/azure/<tenant UUID>/events`.
- Delivery schema: **Event Grid Schema**.
- Included event type: `Microsoft.Communication.EmailDeliveryReportReceived`.
- Advanced filter: `data.sender` **StringIn** the configured sender address. This is required when an ACS resource serves multiple senders; batches containing another sender are rejected.
- Subscription name matching `SubscriptionName` above.
- Static secret delivery header `X-Aonik-Integration-Key` with the same secret, marked secret in Event Grid.

Event Grid supplies `aeg-subscription-name` and `aeg-event-type`. The receiver requires the configured secret, tenant route and subscription name before reading the body, then validates every source topic and sender before processing the batch. It ignores user-supplied tenant headers. The secret is compared using a fixed-time hash comparison. Notifications are capped at 100 events / 256 KiB. Unsupported JSON/schema/source payloads are rejected; unknown message IDs are acknowledged without exposing whether another tenant owns them.

The synchronous subscription-validation handshake echoes `validationCode` only for the authenticated expected topic/subscription. It never follows `validationUrl`. Microsoft documents [custom secret headers](https://learn.microsoft.com/en-us/azure/event-grid/delivery-properties) and the [validation handshake](https://learn.microsoft.com/en-us/azure/event-grid/end-point-validation-event-grid-events-schema). Configure HTTPS and provider retry/dead-letter monitoring as part of deployment. Secret or sender changes must be coordinated with the Event Grid subscription.

## History and designs

The existing tenant-scoped history/status APIs return `deliveryStatus` and `deliveryUpdatedAt`. Statuses are Delivered, Bounced, Suppressed, Quarantined, FilteredSpam, Failed, Expanded, AwaitingReport, NotTracked and NotSent. The UI includes shared Delivered/Bounced SVGs, semantic status badges and UTC report times. It does not poll ACS or send email when refreshing history. Existing customer read permissions apply; send/resend permissions are unchanged.

Editable JSX artboards include a dedicated **Receipt delivery** scene and delivery columns on the other receipt history scenes. Run `node Templates/aonik-admin-starterkit/scripts/verify-remittance-artboards.mjs` to verify all twelve scenes and regenerate 24 theme previews. The production UI verification script captures `remittance-receipt-delivery-{light,dark}@2x.png` using fictional provider results and writes `receipt-delivery-report.json`. No real email or Azure resource is needed for these tests.

No schema migration is required. Ledger balances, payout execution and transfer financial state are unaffected by delivery notifications.

## Review a bounced customer email

Writing staff can select **Review email** on a Bounced history row. The server resolves the exact saved contact ID from the immutable receipt request, scoped to the current transfer and tenant. It does not infer an address from the masked recipient. The review dialog displays the current saved email and warns when it differs from the address used at send time.

After verifying the address with the customer, the operator can save a correction. This updates that existing contact for future communications, preserves its primary flag, and records `CustomerEmailUpdated` with the actor, contact and receipt references plus address hashes. The contact and audit save together through the Platform context. Raw addresses are not copied into this audit record. The existing contact rowversion detects concurrent edits during a save; an expected-current-email check rejects stale forms. Invalid addresses and another email already saved on the same customer are rejected. Reload the saved email after a conflict or uncertain save response before continuing.

Saving an email does not send a receipt or alter earlier receipt/delivery evidence. It opens the existing resend flow, which reloads the saved contacts, requires recipient selection and a separate send confirmation, and reserves a new send request UUID. Existing unresolved-send recovery protections still apply. Operators can continue without editing after confirming the current address. If the original contact has been removed, use **Resend receipt** to choose another saved recipient.

The staff-only endpoints are:

- `GET /remittance/{id}/receipt/email/{requestId}/recipient` — current saved contact for the bounced request.
- `PUT /remittance/{id}/receipt/email/{requestId}/recipient` — `{ "expectedEmail": "current@example.test", "newEmail": "corrected@example.test", "confirmUpdate": true }`.

Both require `AdminWritePolicy`, `Payment.Read`, `Customers.Read` and `Customers.Write`. The service checks these permissions, transfer ownership and current Bounced status again; the Platform editor also checks tenant/customer contact ownership and customer write permission. Read-only staff see history without review or resend actions. Responses containing contact details use `Cache-Control: no-store`.

The twelfth editable scene, **Review customer email**, mirrors the save-then-resend workflow with local fictional state. Production light/dark captures are named `remittance-review-receipt-email-{light,dark}@2x.png`. Browser checks cover cancellation, invalid input, confirmation reset, stale-save recovery, no dispatch on save, the fresh recipient confirmation, tenant context and read-only access.
