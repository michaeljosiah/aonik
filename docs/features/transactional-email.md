# Transactional email

Commerce order confirmation uses the existing `PaymentCompletedEvent` handler and transactional outbox. The handler first converges the recorded checkout payment, inventory, cart and order, then calls the Platform templated-email adapter through a small SharedKernel contract. There is no new queue, mail table, notification router or scheduler.

## Order confirmation

Only the payment intent recorded on `OrderChargeSummary` can complete a checkout. Missing or unrelated intent IDs change nothing. Completion accepts the existing Draft/legacy PendingFunding order states, or retries a partially completed order; an already Complete order needs no transition. Later order states and post-capture refund statuses are not overwritten by an email retry. A needed transition uses the existing expected-status check.

The receipt requires the recorded payment to be Captured, the cart to be CheckedOut, and the order to be Complete. It goes to the immutable purchaser email in `OrderDeliveryDetails`. The allowlisted model contains the order ID, stored charge breakdown, purchased SKU/quantity/price facts, box selections and their plain personalisation summaries, and the recorded delivery address/date/timezone/notes. It does not read a mutable account email or current catalogue names, and contains no cart token, payment client secret, provider redirect URL or raw metadata. Human order numbers and fuller historical labels remain #360.

Legacy/generic carts without a delivery snapshot have no known receipt address and are skipped with an order-ID log. A box missing its required snapshot fails for operational investigation rather than guessing a recipient. No customer account is created by sending a receipt.

Provider and rendering failures escape the handler, leaving the existing outbox retry/backoff/dead-letter policy in charge. A successfully committed handler inbox entry suppresses ordinary redelivery. External sending and the inbox commit are not one transaction: a crash after provider acceptance can cause a duplicate email. Provider acceptance is not proof of arrival in the customer's mailbox. Captured payment/order state is preserved during email failure; a retry does not create another payment or order.

## Templates and branding

Four shared defaults are inserted idempotently through the existing notification-template seeder:

| Key | Producer |
| --- | --- |
| `commerce.order-confirmation` | Matching Commerce payment completion |
| `identity.account-setup-access` | Template ready; secure link flow remains #350 |
| `identity.password-reset` | Template ready; active identity-provider flow remains separately configured |
| `identity.email-change-confirmation` | Template ready; verify-before-change flow remains #350 |

Gift-card delivery and purchaser receipts remain #364. The identity defaults do not issue links or send decorative second reset emails. Their future producers must supply a real `action_url` from an approved HTTPS storefront origin and the actual `expires_at`; template HTML escaping is not URL authorization.

The adapter reuses `NotificationTemplateService` and `IEmailSender`. Existing tenant templates and optional base/override bindings still apply. New shared defaults use one small HTML wrapper and explicit Liquid escaping for supplied values. Tenant-authored overrides retain responsibility for their own rendering.

The reserved `brand` dictionary comes only from the explicitly published `Business.PublicProfile`: display name, logo, website and public contact details. Callers cannot replace it. With no published profile, the shared wrapper uses AONIK and omits unknown details. Private tenant administration contacts are never fallback branding. This fallback does not make an unconfigured Abby's Table deployment branded or launch-ready.

## Sender configuration

The existing ACS sender reads these canonical settings through `TenantFirstSettingReader` at send time:

- `Communication.Email.Provider`
- `Communication.Email.AzureCommunicationServices.ConnectionString`
- `Communication.Email.AzureCommunicationServices.FromAddress`

Each key uses the current tenant's stored value first, then the existing global/configuration/default resolution. Legacy global `Communication.Azure.ConnectionString` and `Communication.Azure.Email.FromAddress`, followed by the existing options, remain compatible fallbacks. Shared ACS credentials can be global while each tenant supplies its verified sender address. The new templated adapter never supplies a caller-selected From address.

Canonical credentials use the existing encrypted settings storage. Do not commit credentials. The old synchronous `IEmailSender.IsConfigured` property remains an options-only probe; the new adapter does not use it as a runtime readiness gate. Empty committed appsettings and that probe alone cannot establish whether deployed settings work. The communication-provider administration screen writes global settings; tenant sender overrides use the existing tenant-settings administration path.

Sender logs use provider operation ID/status instead of recipient/content. Client-initialization failures have a generic diagnostic so connection strings cannot leak through exception messages.

## Deployment work still required for #349

1. Identify the approved Abby tenant and ACS resource/domain; verify and link the sender domain and configure its FromAddress and credentials through the deployed settings/secret system. No resource, domain or address is assumed by this code.
2. Run the existing `NotificationTemplates` global seed contributor, for example through authorized `POST /admin/data-seeds/run` with `{"keys":["NotificationTemplates"]}`, and inspect its returned operations for errors. Startup seeding also runs when the existing `Database:SeedData` configuration enables it. Existing authored templates are not overwritten, and a code merge alone does not seed a deployed database.
3. Publish the approved business profile and any tenant template/base overrides, and ensure the Worker drains the outbox and operations can inspect retries/dead letters.
4. Configure the active identity provider's sender and branding consistently. Keycloak/Auth0 reset adapters delegate email to that provider; a Fluid template does not replace its SMTP/email theme. Verify the deployed B2C flow separately if it is selected. Account access and email-change links depend on #350.
5. Validate actual payment-confirmation and identity email flows with an approved test mailbox. Unit tests mock the provider and do not establish live delivery.

#349 remains open until these operational steps and its downstream link flows are verified. #192/#194 remain separate platform issues; this slice does not claim that generic in-app notification requests now implement every channel.

## Validation

Tests exercise the real Fluid renderer and template resolver, shared seeding, tenant overrides and brand isolation, escaped content, recorded recipient/charge projection, exact payment-intent guards, completion retries and refund/cancellation preservation. Real dispatcher tests prove that send failure leaves no completed inbox entry, a successful retry can commit it, and normal redelivery then skips the handler. ACS SDK mocks cover tenant settings, legacy fallbacks, content, cancellation and failures without live sends. No schema migration is required.
