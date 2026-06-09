# ADR-010: Partner-Owned Connector Credentials

**Status**: Proposed
**Date**: 2026-06-09
**Decision Makers**: Development Team
**Related**: [ADR-005](005-adopt-module-first-modular-monolith.md), [Spec 031](../specifications/031.partner-integration-abstraction.html) (partner abstraction), [Spec 037](../specifications/037.flutterwave-partner-connector.html) (Flutterwave v4 connector), [Spec 038](../specifications/038.admin-partner-connector-configuration.html) (admin connector config), [Spec 040](../specifications/040.partner-biller-catalogue-import.html) (v3 bills import), [Spec 042](../specifications/042.partner-connector-credential-rehoming.html) (implementation of this ADR)
**Revisions**: Rev 1 (2026-06-09) — incorporated implementation review. `CredentialBundle` promoted to a **first-class entity** (the settings store only encrypts statically-defined keys, so a dynamic bundle key would persist in plaintext); resolution **binds to the persisted `Connector` row** and propagates `ConnectorId` to downstream records; the legacy-key fallback now **fails closed**.

## Context

The partner abstraction (Spec 031) has landed, and with it the first real connector — Flutterwave, split across a **v4 OAuth payout** connector (Spec 037) and a **v3 secret-key bill-payment** connector (Spec 040). Configuring those connectors exposed a modelling tension that will only worsen as the partner roster grows (eTranzact, Wise, iPay are next).

**Connector credentials currently live in two disconnected places:**

1. **A provider-singleton settings silo.** `FlutterwaveConfigProvider` (v4) and `FlutterwaveBillsConfigProvider` (v3) each read `ISettingProvider` keyed by **fixed, global** keys from `PartnerGatewaySettingNames`:
   - `Finance.Partners.Flutterwave.{Enabled,BaseUrl,IdpTokenUrl,ClientId,ClientSecret,EncryptionKey,DefaultTransferPurpose}`
   - `Finance.Partners.Webhooks.Flutterwave.SigningSecret`
   - `Finance.Partners.Flutterwave.Bills.{Enabled,BaseUrl,SecretKey}`

   The Admin UI surfaces the v4 set at **Settings → Payment Gateways → Flutterwave**; the v3 bills set has **no UI at all** (env / DB-settings only). There is exactly one credential set per provider, per tenant.

2. **A per-partner connector entity.** The **Partner Network → Partner → Connectivity** tab lets an operator add a `PartnerConnector` carrying `{ ConnectorType, Status, CredentialsRef, ConfigJson }`, under the explicit guidance: *"CredentialsRef points to gateway settings; never paste secrets here."*

These two representations do not agree on who owns connector configuration. The connector **resolution** path (`IPartnerConnectorResolver` → `ResolvePayoutConnector` / `ResolveBillPaymentConnector`) keys on a global provider code and reads the provider-singleton settings; the `PartnerConnector.CredentialsRef` is a parallel pointer that the config providers do not actually consume. The "points to gateway settings" note is an admission of the split rather than a resolution of it.

**Why the provider-singleton assumption breaks.** Credentials are an attribute of *an account*, and accounts are held *with partners*. One global credential set per provider cannot express:

- **Multiple accounts of the same provider** (e.g. separate Flutterwave entities per country/regulatory boundary).
- **Per-partner credential isolation** — a partner's secret must not be readable or rotatable as a side effect of touching another's.
- **A growing roster** where every partner (Flutterwave, eTranzact, Wise, iPay) carries its own credentials and its own enable/rotate lifecycle.

**Secondary symptoms of the same root cause:**

- Spec 040's headline feature (biller catalogue import) depends on the v3 bills connector, which has **no operator-facing config path** — its secret key is set only via environment variables or direct DB-settings writes.
- The gateway-settings page and the partner Connectivity tab present two competing answers to "where do I configure Flutterwave?"

**An unresolved identity question underlies all of this:** *is Flutterwave a Partner, or a rail reached through a Partner?* The current seed models **"Gold Coast Bill Hub"** as the partner with Flutterwave as a connector *under* it. For the Payabo UK→NG remittance launch, Flutterwave is itself the rail that both moves money and exposes billers, which argues for **Flutterwave being modelled as a Partner**.

## Decision

**Connector credentials are logically owned by the partner's connector instance. Secret bytes remain in a central encrypted store, addressed by `CredentialsRef`. The partner is the single home for connector configuration.**

Concretely:

- A **Partner** (Flutterwave, eTranzact, Wise, …) has **one or more Connectors**. A connector instance carries `{ ProviderType, Enabled, ConfigJson, CredentialsRef }` — non-secret config (base URL, country, transfer purpose) inline; the secret addressed by reference.
- `CredentialsRef` resolves to a **named credential bundle** — a **first-class, tenant-scoped `CredentialBundle` entity** whose secret fields are encrypted with explicit `IDataProtection`. It is deliberately **not** a settings-store convention: `SettingService` only protects keys carrying a static `SettingDefinition.IsEncrypted` flag (`SettingService.cs:258`), so a dynamic bundle key would persist in plaintext. A Key Vault move is deferred (same stance as [ADR-007](007-keycloak-as-auth-provider.html)). Secrets are **write-only** and never returned by any read API — the convention already proven on the Payment Gateways page (`hasXxx` / "Configured" badges, never values).
- **Connector resolution keys on `(tenant, partner, providerType)` → connector → bound bundle**, not a global provider-singleton key.
- **Settings → Payment Gateways is reframed** from "the global Flutterwave config" into the **credential-bundle manager** that `CredentialsRef` resolves to — or credential entry folds directly into the partner Connectivity tab with a write-through to the central store. Either way, the provider-singleton assumption is removed.
- **Integration providers are modelled as Partners.** A single Flutterwave partner owns both the `flutterwave-payout-v4` and `flutterwave-bills-v3` connector instances, each with its own bundle.

### Architectural Guarantees

1. **Credentials follow the account; the account follows the partner.** No configuration concept is keyed by provider code alone. Two Flutterwave accounts are two partners (or two connector instances), each with its own bundle.
2. **Secrets are write-only and centrally stored; the partner connector holds only a reference plus non-secret config.** No raw secret is ever persisted on a partner/connector row or returned by a read API.
3. **One partner, many connectors.** v4 payout and v3 bills are distinct connector instances under one Flutterwave partner, with distinct credential bundles (OAuth client vs `FLWSECK-` secret key). The v3 bills connector thereby inherits the Connectivity UI, closing the Spec 040 no-UI gap.
4. **Resolution binds to a persisted connector row.** `(tenant, partner, providerType)` resolves to a specific `Connector` row — which already carries `Id`, `PartnerId`, `ConnectorType`, `CredentialsRef`, `ConfigJson` (`Entities/Partners/Connector.cs`) — and returns a runtime connector bound to *that* row's bundle, not a tenant-global singleton. The row's `Id` (**`ConnectorId`**) then propagates onto every downstream record the connector creates (beneficiary registration, payout, transmission, bill payment, webhook correlation) so two accounts of one provider never alias.
5. **Launch is not blocked.** The current provider-singleton path keeps working for a single account; the `CredentialsRef` seam makes the move **additive**, not a rewrite (see Phasing).
6. **Provider type is code, not configuration.** Operators never author transport, auth scheme, or endpoints — those ship with the connector. Operators bind credentials and toggle `Enabled`.
7. **Fallback fails closed.** A partner-specific connector with no bound bundle does **not** silently borrow the legacy global account. The legacy-key fallback applies **only** to an explicitly-migrated default connector; any other unconfigured connector fails the call rather than rerouting money through the wrong account.

### Phasing

- **Phase 0 — Launch (now).** Ship the Payabo UK→NG remittance launch on the existing provider-singleton settings. One Flutterwave account; `Finance.Partners.Flutterwave.*` keys are sufficient. Do **not** block launch on this ADR.
- **Phase 1 — Re-home (Spec 042, post-launch).** Introduce the credential bundle + `CredentialsRef` resolution, migrate the existing provider-singleton values into a seeded default bundle bound to the Flutterwave partner's connectors, and reframe the Payment Gateways page. Keep the old keys as a read fallback during transition.
- **Phase 2 — Generalise.** Onboard the next connector (eTranzact / Wise / iPay) directly on the partner-owned model with **no** new global settings keys, proving the abstraction.

## Consequences

**Positive**

- Scales to N partners and multiple accounts per provider, with per-partner credential isolation, rotation, and audit.
- Collapses two competing config surfaces into one coherent home (the partner).
- Closes the Spec 040 gap: v3 bills gains an operator-facing config path for free.
- Centralised, write-only secret handling is preserved and made consistent across all connectors.

**Negative / cost**

- Refactor of `FlutterwaveConfigProvider` and `FlutterwaveBillsConfigProvider` to resolve by `CredentialsRef` instead of fixed keys.
- Change to `IPartnerConnectorResolver` resolution semantics (partner-aware).
- Admin UI work: a credential editor on the partner Connectivity tab and the reframing/retirement of the standalone Payment Gateways page.
- A data migration: existing provider-singleton settings → a seeded default bundle, plus a back-compat read fallback during transition. Any schema change must be **tool-generated** per the migration discipline in `CLAUDE.md`.

**Neutral**

- Physical secret storage stays central (settings store now, vault later). Only the **logical ownership** and the **addressing** change — this is not a "secrets move to the partner row" decision.

## Alternatives Considered

- **A. Keep provider-singleton global gateway settings.** Rejected: cannot express multiple accounts or per-partner isolation, and leaves the gateway page and Connectivity tab permanently disconnected.
- **B. Store raw secrets as columns on the `PartnerConnector` row.** Rejected: violates the write-only / never-returned secret hygiene already established, forfeits central rotation and audit, and risks leaking secrets into snapshots and logs.
- **C. Hybrid — partner connector owns the reference + non-secret config; a central store owns the bytes (this ADR).** Chosen: matches the shape the Partner Network module already reaches for via `CredentialsRef`, scales cleanly, and keeps secret handling centralised.

## Open Question

**Is Flutterwave a Partner or a rail under a Partner?** **Resolved for this phase** ([Spec 042](../specifications/042.partner-connector-credential-rehoming.html) §17): Flutterwave is modelled as a **rail Partner** — a Partner record whose connector instances move money and expose billers, with `ConnectorId` hanging off that rail partner. The biller-hub abstraction (a partner reached *through* a rail, e.g. "Gold Coast Bill Hub") remains a future, coexisting model; revisit when a biller-hub partner is first onboarded.
