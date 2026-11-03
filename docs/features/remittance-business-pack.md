# Remittance business pack and admin profile

The `remittance` business type is an installed configuration pack and a dedicated Admin UI profile. Select **Remittance** when creating a tenant, or supply `businessType: "remittance"` in the existing tenant-creation request. A platform administrator retains the general host profile when inspecting that tenant; the focused profile is for tenant administration.

The pack is version 1 at `src/Aonik.SharedKernel/Abstractions/Packs/Data/remittance.pack.json`. SharedKernel and the CLI discover it through their existing embedded-resource globs. No database schema or new product-specific platform symbols are required.

## Provisioned configuration

The pack declares `finance`, `ai`, `agents` and `documents`. Initial provisioning expands the catalogue's hard dependencies and core modules to this effective set:

| Enabled module | Purpose |
| --- | --- |
| Platform | Tenant identity, parties, access and settings |
| Ordering | Core business-intent spine; Finance's hard dependency |
| Finance | Existing orders adapter, ledger, payments, pricing and partner network |
| AI | Route policies, models, prompts and AI runs |
| Agents | Existing domain-agent orchestration, approvals and proposals |
| Documents | Document management behind the Compliance entry |

Commerce, Personal Finance, Groups, Subscriptions, Workspaces and Voice are off for a newly provisioned tenant. The Admin UI's local `workspace` screen is separate from the backend `workspaces` module and remains available through the profile.

The sole tenant setting is `Branding.AgentDisplayName = Remittance`. There are no agent overrides, fixed model IDs, sample transactions, preconfigured partners, corridors or FX rates. The existing module contributors handle their normal provisioning resources, including Finance's ledger/account setup. The pack does not grant permissions or weaken AI tool approvals.

Existing applier semantics remain authoritative: settings are insert-only; a later application preserves explicit operator module choices and can widen pack-owned modules without narrowing an existing tenant. This change does not modify any live tenant. Provision or re-provision the intended tenant through the existing operational workflow.

Inspect the installed manifest offline:

```powershell
dotnet run --project src/Aonik.Cli -- packs list --output json
dotnet run --project src/Aonik.Cli -- packs show remittance --output json
```

## Navigation and route coverage

`src/Aonik.AdminUi/src/config/admin-profiles/remittance.json` selects `/orders/activity` as the preferred landing route and labels it **Transfer orders**. Its menu sections are:

- **Money movement:** Transfer orders, New transfer, Beneficiaries, Customers, Approvals.
- **Network & treasury:** Corridors, Partners, FX & Rates, Ledgers.
- **Administration:** Compliance, Team (Users, Roles, Permissions), AI & Agents (Agents, Tasks, Usage), Settings.

Supporting routes are included for customer, partner, user and agent detail; Compliance's document redirect/list/detail and authorised document creation; chart of accounts and journal entries; AI chat; module settings; and the local workspace. Those routes are not additional top-level menu items. The shared Remittance transfer-order glyph is used in the sidebar and command palette.

All menu and route visibility still intersects the profile with the server manifest's module enablement, permissions, policies, audience and disabled-route/nav overrides. For example, Partners requires `Catalog.Read` and `AdminPolicy`; ledger views require `Ledger.Read`; access management retains its own permissions. Disabling Documents hides both Compliance and its destination. Disabling Finance hides its screens and makes Customers the first available landing destination. The profile does not grant API access.

The profile excludes Commerce/storefront, billing/invoices, biller catalogues, personal accounts, bill-payment authoring and host tenant administration. The Orders page suppresses the bill-payment filter and disables bill-payment detail links when the corresponding detail screen is unavailable, avoiding links to routes the profile does not permit.

## Capability boundary

This is configuration for existing platform services and operator screens. **Transfer orders is a presentation label for the existing unified order list**; it remains a unified list, with a Remittance filter and links to dedicated transfer details. The [transfer editor](remittance-transfer-editor.md) adds an explicit operator quote/review/confirm journey using existing execution services. [Beneficiary management](remittance-beneficiaries.md) adds customer-scoped destination verification, notes and individual removal. No new payment rail, screening integration, partner connector or specialised Remittance Agent is implemented by installing the pack. Configure and verify the required operational services separately before using them for live transfers.

Orders continue to capture business intent; payments and payouts execute it, and immutable double-entry ledger records provide financial evidence. Existing financial agent tools retain server approval gates and high-risk durable proposal/dispatcher execution. Model routing and AiRun auditing are unchanged.

## Starter kit and validation

The starter kit now reads the installed pack/profile through `scripts/sync-design.mjs`, including the module catalogue's core and dependency closure. The separate proposed Remittance JSON files have been removed. Gallery labels and screenshots identify Remittance as installed configuration; their records and figures remain fictional marketing data. The Food Commerce fixture also now includes Finance through Commerce's hard dependency, correcting its earlier assumption that omitted declared modules were disabled.

Coverage includes embedded pack discovery, canonical IDs and module closure; initial module rows and preservation of explicit overrides; idempotent branding/version application; offline CLI inspection; profile landing/labels/detail routes; excluded products; permission/policy/module filtering; host isolation; and shared icon mappings. No migrations or live financial writes are involved in these checks.
