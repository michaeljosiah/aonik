---
title: Configure Payabo for a tenant
description: Step-by-step tenant configuration for Payabo — currency, countries, feature flags, Plaid, voice, agents.
sidebar_label: Configure Payabo
sidebar_position: 2
---

# Configure Payabo for a tenant

:::info
Turn a freshly bootstrapped tenant into one that can serve Payabo end-users. Covers tenant settings, feature flags, Plaid, voice, and the AI agents Payabo relies on.
:::

## Why this matters

There is no single "enable Payabo" switch. Payabo is the sum of a tenant's currency and country settings, a set of personal-finance feature flags, an account-linking provider, an AI route policy, and (for the mobile app) a Firebase project. This page walks you through each piece once.

## Before you start

- You have completed the [Quickstart](../../getting-started/quickstart.md) and have one tenant + a platform admin user.
- You have an [identity provider configured](../../identity-access/index.md) so the admin user can sign in to the Admin UI.
- You have decided whether you are running Plaid in **sandbox** (for development) or **production**.
- You know your tenant's primary currency (ISO 4217) and the country codes (ISO 3166-1 alpha-2) you intend to support.

## Steps

### 1. Set the tenant's currency and countries

Sign in to the Admin UI (`http://localhost:5173` in local development) and open **Settings → General**.

Set:

- **Default currency** — e.g. `NGN`, `KES`, `GBP`. Stored as `Tenant.DefaultCurrency`.
- **Supported countries** — ISO codes the tenant operates in. Stored as JSON on `Tenant.SupportedCountriesJson`.
- **Registration countries** — the subset of supported countries that may sign up Payabo users. Stored on `Tenant.AllowedOriginCountriesJson`. If left blank, falls back to the supported set.

These three values control which countries appear in the Payabo registration country picker, served by `GET /host/tenants/{tenantId}/registration-countries`.

### 2. Enable Payabo's personal finance feature flags

Still in the Admin UI, open **Tenant Setup Wizard → Step 3 (Features)** (or call `PUT /admin/tenants/{tenantId}/features` directly).

The feature flag namespace is `PersonalFinance.*`. Toggle on the slices Payabo needs:

| Feature flag                                | Enables in Payabo |
| ------------------------------------------- | ----------------- |
| `PersonalFinance.Budgets.Create`            | Creating budgets |
| `PersonalFinance.Budgets.Tracking`          | Budget vs. actual on the dashboard |
| `PersonalFinance.Goals.Create`              | Creating savings goals |
| `PersonalFinance.Goals.Tracking`            | Goal progress on the dashboard |
| `PersonalFinance.Subscriptions.Detection`   | AI-proposed subscription graph edges |
| `PersonalFinance.Subscriptions.Tracking`    | Subscription list in the UI |
| `PersonalFinance.Bills.Reminders`           | Upcoming-bill notifications |
| `PersonalFinance.Bills.AutoPay`             | Auto-pay flows |

Feature flags are stored per tenant in the `TenantFeature` table. You can re-enable or disable them at any time without redeploying.

### 3. Wire Plaid for account linking

Plaid credentials are platform-level today (not per-tenant): set them once in your API deployment configuration and they apply to every tenant.

In your environment (user-secrets, environment variables, or `appsettings.{env}.json`), set the `Finance:PersonalFinance:Plaid` section:

```json title="appsettings.{env}.json"
{
  "Finance": {
    "PersonalFinance": {
      "Plaid": {
        "UseRealPlaidApi": true,
        "BaseUrl": "https://sandbox.plaid.com",
        "ClientId": "<your-plaid-client-id>",
        "Secret": "<your-plaid-secret>",
        "CountryCodes": ["GB"],
        "Products": ["transactions"]
      }
    }
  }
}
```

Set `UseRealPlaidApi` to `false` to use Aonik's built-in simulator (useful for local development without a Plaid account). The simulator returns deterministic accounts and transactions.

A dedicated [Integrations → Plaid](../../integrations/index.md) page (shipping in Phase 2 of the docs rewrite) covers redirect URIs, webhook signing, and recurring transaction sync in depth.

### 4. Configure the voice and TTS profile

Payabo's mobile app talks to a tenant-specific voice profile. Open **Settings → Speech** and set:

- **Provider** — e.g. ElevenLabs, Azure Speech
- **Voice ID** — your chosen voice for tenant agents
- **API credentials** — vendor-specific

The profile is stored under the setting key `Platform.TextToSpeech.TenantProfile`. The mobile app fetches it through `GET /tenants/me/voice-settings` and streams audio over `/ai/voice`.

You can preview a voice with `PreviewTenantTextToSpeechEndpoint` from the Admin UI before saving.

### 5. Choose the AI route policy for the Personal Finance agent

Personal Finance is one of Aonik's domain agents. Decide which model provider serves it for this tenant — usually you'll route to your preferred frontier model. Set the route policy in your AI provider configuration (see the AI Platform section, shipping in Phase 4 of the docs rewrite).

If you haven't configured any AI provider yet, Payabo chat and voice will surface a friendly error and the rest of the product (dashboard, accounts, transactions) will continue to work.

### 6. Configure the mobile shell (if you ship Payabo mobile)

The Flutter app at `apps/payabo_mobile` currently bakes Firebase config into `lib/firebase_options.dart`. To ship a branded mobile build you will:

- Replace the Firebase project (`payabo-b62c7`) with your own
- Re-generate `firebase_options.dart` via the FlutterFire CLI
- Update push notification certificates in your Firebase Console
- Update deep-link domains for sign-in callbacks

Multi-tenant mobile config is on the roadmap; today the mobile shell is single-tenant per build.

### 7. Verify the configuration

The fastest end-to-end check:

1. Visit the Payabo web shell at `http://localhost:5174`.
2. Sign up as a new user from one of the tenant's registration countries.
3. Complete phone OTP verification.
4. Try to connect a Plaid sandbox account.
5. Open the chat surface and ask the assistant about your spending.

A green path on all five steps means the tenant is correctly configured for Payabo.

## Troubleshooting

### Registration country picker is empty

**Symptom.** The Payabo sign-up page lists no countries.

**Cause.** `Tenant.AllowedOriginCountriesJson` is set to `[]` and `Tenant.SupportedCountriesJson` is also empty.

**Fix.** Add at least one ISO country code to **Settings → General → Supported countries** (and registration countries, if you want a stricter subset).

### Plaid Link returns an error immediately

**Symptom.** The Plaid Link sheet opens and immediately shows "Unable to load."

**Cause.** Either `Finance:PersonalFinance:Plaid:ClientId` / `Secret` are wrong, or the `CountryCodes` array does not include the country the test user is signing up from.

**Fix.** Verify the credentials match the environment (`sandbox` vs `production`) and include every country you registered with Plaid.

### Voice preview button stays grey

**Symptom.** The **Settings → Speech** voice preview button is disabled.

**Cause.** The TTS provider credentials are missing or invalid.

**Fix.** Re-enter the API key and reload. The preview button is gated by a server-side health check against the provider.

### Feature toggle changes don't appear

**Symptom.** You toggle `PersonalFinance.Budgets.Create` on, but the web app still hides the Budgets UI.

**Cause.** The Payabo web app caches feature flags client-side for the session.

**Fix.** Sign the test user out and back in. Server-side checks update immediately.

## What's next

- [Payabo overview](./index.md) — capabilities and module footprint
- [Identity & Access](../../identity-access/index.md) — get logins working
- [Integrations](../../integrations/index.md) — Plaid, ElevenLabs, AI providers in depth
