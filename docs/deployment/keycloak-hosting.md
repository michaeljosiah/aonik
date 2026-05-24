# Hosting Keycloak for an Azure-deployed Aonik

[Spec 029](../specifications/029.keycloak-auth-provider.html) made Keycloak a first-class identity provider option for Aonik. The local-dev story is a single env var — `AONIK_AUTH_PROVIDER=Keycloak` makes Aspire spin up a Keycloak 26 container alongside the rest of the stack. **Production Azure deployments are different.** Aonik's IaC (`iac/azure/stacks/aca/main.bicep`) does not bundle a Keycloak instance by default. This page explains why, and what your options are.

## TL;DR

For an Azure deployment that uses Keycloak as its auth provider, **Aonik does not provision Keycloak for you**. You bring your own Keycloak instance and point Aonik at it via the `AUTH__KEYCLOAK__*` settings. Three realistic paths:

| Option | Best for | Aonik IaC change? |
|--------|----------|-------------------|
| **A. Managed Keycloak SaaS** (Cloud-IAM, Phasetwo, RedHat SSO) | Most production deployments | None |
| **B. Self-hosted Keycloak** in a separate Azure resource group | Operators that need data residency, custom networking, or already run Keycloak | None |
| **C. Future opt-in bundled Aonik module** | Small teams who want zero external setup | Not yet built — see [Future work](#future-work-opt-in-bundled-module) |

[ADR-007](../decisions/007-keycloak-as-auth-provider.md#decision) explicitly excludes "Keycloak hosting / packaging" from Aonik's scope. The reasoning is in the next section.

## Why we don't bundle Keycloak in Aonik's IaC (by default)

Production Keycloak isn't just a container — it's a meaningful piece of identity infrastructure. Bundling it into Aonik's IaC would mean Aonik's deploy stack now owns:

| Concern | What it means in practice |
|---------|--------------------------|
| **Persistent backing store** | Keycloak needs PostgreSQL. Aonik's SQL stack is SQL Server. A bundled Keycloak adds a Postgres dependency (Azure Database for PostgreSQL Flexible Server, ~$30–$80/month minimum), plus its own backup/restore lifecycle. |
| **Realm versioning** | Realm imports (`--import-realm`) run only on a Keycloak container's *first* start. Subsequent realm changes must be applied via the Admin API or `kc.sh export` / re-deploy, which is operator-specific. |
| **Secret management** | The `aonik-admin` service-account secret rotates independently from Aonik's other secrets. Bundling it means Aonik's Key Vault now holds a secret whose lifecycle is governed by Keycloak's own admin flows. |
| **HA + DR** | A production Keycloak in front of multiple Aonik instances needs at least two replicas, sticky sessions or external session storage, regional failover for the Postgres, and a documented restore RPO/RTO. None of that lives in Aonik today. |
| **Federation** | Customers running Aonik often want Keycloak to federate to *their* corporate IdP (Okta, Entra, AD FS, SAML). That config is operator-specific — Aonik can't ship a generic federation policy. |
| **Version upgrades** | Keycloak's major versions ship breaking changes (migrations on the DB, sometimes API contract shifts). The upgrade cadence belongs with whoever owns the realm data, not Aonik's release schedule. |

These aren't blockers — they're real concerns that need a deliberate design pass. ADR-007 chose to defer them rather than make Aonik's IaC implicitly responsible for an identity stack.

## Option A — Managed Keycloak SaaS (recommended for most teams)

Vendors like Cloud-IAM, Phasetwo, and RedHat SSO host Keycloak for you. You get a realm URL, an admin console, backup/restore, and version upgrades managed by the vendor. The integration with Aonik is unchanged from the [Keycloak operator runbook](../operations/keycloak-setup.md) — create the two clients (`aonik-spa` + `aonik-admin`), wire the mappers, then set Aonik's settings.

**Cost.** Typical entry-tier is $50–$200/month for a development realm. Production tiers with SLAs sit around $300–$1500/month depending on user count and HA tier.

**Trade-off.** You pay for someone else to operate the realm. You can still federate to your corporate IdP through the managed realm — federation is a Keycloak feature, not a hosting feature.

## Option B — Self-host Keycloak in a separate Azure resource group

If your org has data-residency requirements, an existing Keycloak deployment, or strong preferences about how identity infra is operated, run Keycloak yourself.

A minimal Azure self-hosted setup:

```text
┌────────────────────────────────────────────────────────────────┐
│ Resource group: aonik-identity-<env>                            │
│                                                                  │
│  ┌──────────────────────┐    ┌─────────────────────────────┐   │
│  │ ACA / App Service     │    │ Azure Database for          │   │
│  │ quay.io/keycloak/     │◄──►│ PostgreSQL Flexible Server  │   │
│  │ keycloak:26           │    │ (Burstable B1ms, ~$15/mo)   │   │
│  │                       │    │                              │   │
│  │ FQDN: keycloak.       │    │ Storage encrypted at rest    │   │
│  │   <env>.aonik.dev     │    │ Daily backups, 7-day retain  │   │
│  └──────────┬───────────┘    └─────────────────────────────┘   │
│             │                                                    │
│  ┌──────────▼───────────┐                                       │
│  │ Key Vault            │                                       │
│  │ - kc-admin-password  │                                       │
│  │ - kc-db-password     │                                       │
│  │ - aonik-admin-secret │                                       │
│  └──────────────────────┘                                       │
└────────────────────────────────────────────────────────────────┘
                              ▲
                              │ HTTPS (operator-managed cert,
                              │ Azure-managed cert, or Front Door)
                              │
┌────────────────────────────────────────────────────────────────┐
│ Resource group: aonik-<env>  (Aonik's existing stack)           │
│  api / worker / adminui / qdrant / SQL / KV / ACA env           │
│  AUTH__KEYCLOAK__AUTHORITY = https://keycloak.<env>.aonik.dev/  │
│                              realms/aonik                        │
└────────────────────────────────────────────────────────────────┘
```

**Provisioning checklist:**

1. Create a new resource group `aonik-identity-<env>`
2. Provision an Azure Database for PostgreSQL Flexible Server, B1ms tier or higher, with a `keycloak` database and a dedicated admin user
3. Provision a Container App for Keycloak — `quay.io/keycloak/keycloak:26`, command `start`, env vars: `KC_DB=postgres`, `KC_DB_URL=...`, `KC_DB_USERNAME=...`, `KC_DB_PASSWORD=<KV reference>`, `KC_HOSTNAME=<your FQDN>`, `KC_HOSTNAME_STRICT=true`, `KC_PROXY=edge`, `KC_HTTP_ENABLED=true`
4. Create the `aonik` realm via the admin console (or post the [`infra/keycloak/realm-export.json`](../../infra/keycloak/realm-export.json) via `kc.sh import`)
5. Configure the two clients (`aonik-spa` + `aonik-admin`) per the [operator runbook](../operations/keycloak-setup.md)
6. Set Aonik's GitHub Actions repo variables — `VITE_AUTH_PROVIDER=keycloak`, `VITE_KEYCLOAK_AUTHORITY`, `AUTH__KEYCLOAK__AUTHORITY`, etc. (see [Azure deployment → Keycloak example](./azure-deployment.md#auth0-example-individual-variables))
7. Re-deploy Aonik via the normal `cd-deploy` workflow

**Cost.** Expect ~$50–$120/month for the database + Container App at low-volume tiers. HA tiers (multi-AZ Postgres, two Keycloak replicas) push it to $200–$400/month.

**Trade-off.** You own the Keycloak lifecycle — version upgrades, secret rotation, realm versioning, backup/restore drills. The repo gives you `infra/keycloak/compose.keycloak.yml` and the realm export as a reference but does **not** provision the Azure resources.

We do not currently ship a separate Bicep stack for this — `iac/azure/stacks/aca/main.bicep` is the Aonik runtime stack. A `iac/azure/stacks/identity/main.bicep` companion stack is feasible future work and would slot into Option C.

## Option C — Future opt-in bundled module (not yet built)

The user-facing motivation is real: "Aspire's local-dev story makes Keycloak the simplest path, why isn't the cloud-deploy story symmetric?" A bundled IaC module would close that loop.

**What it would look like:**

- A new Bicep module `iac/azure/modules/keycloak.bicep` that provisions an ACA Keycloak container + Azure Database for PostgreSQL Flexible Server (Burstable tier as default)
- Gated by a top-level parameter `enableBundledKeycloak` (default `false`) so existing operators see no behavioral change
- Realm import via init-container or an Azure Files share mounting [`infra/keycloak/realm-export.json`](../../infra/keycloak/realm-export.json) — same artifact the local-dev Aspire profile uses
- Outputs the Keycloak FQDN that the runtime stack pipes into `AUTH__KEYCLOAK__AUTHORITY` automatically
- Provisions admin and DB credentials in Aonik's existing Key Vault
- Documents itself as a "starter — production-acceptable for small teams" rather than "the recommended path for large deployments"

**What it would explicitly not cover (without further work):**

- Multi-region HA, regional failover
- Federation policies (operators wire their own upstream IdP in the realm)
- Realm-data backup/restore drills (Azure DB backups cover the DB; realm-config diffs are operator's job)
- Version upgrades from one Keycloak major to the next

**Why we haven't shipped it yet:**

1. ADR-007 explicitly excludes Keycloak hosting from Aonik's scope. Reversing that needs a deliberate ADR amendment, not an opportunistic addition.
2. The bundled module's design has real choice points — Postgres tier, realm import strategy, secret-rotation policy, HA story — that are worth a spec-level review rather than a feel-good "just add it."
3. The current set of Keycloak operators (count: zero in production, this is a brand-new capability) doesn't yet give us evidence on what they actually want.

**If you want it.** Open an issue with a spec proposal (e.g. spec 031). Concrete asks from operators (data-residency requirements, expected user counts, federation needs) make the design conversation real. The local-dev Aspire integration already proves the realm shape works end-to-end; the cloud module would slot into that same shape.

## What to do today

Given the three options above, the practical recommendation is:

- **Local dev** — use the Aspire-bundled Keycloak (`AONIK_AUTH_PROVIDER=Keycloak` + `pwsh .\scripts\onboard.ps1`). Zero external accounts, zero IaC.
- **Production / staging** — use a managed Keycloak SaaS (Option A) unless your org has specific reasons to self-host (Option B). Aonik's CI/CD already supports both via the [Keycloak settings table](./azure-deployment.md#auth0-example-individual-variables).
- **If your team commits to Keycloak and wants the bundled module** — file a spec-031 proposal and we'll evaluate the trade-offs deliberately rather than ship a half-considered module.

## See also

- [ADR-007 — Keycloak as a first-class auth provider](../decisions/007-keycloak-as-auth-provider.md) — the architectural decisions behind this guide
- [Spec 029 — full Keycloak specification](../specifications/029.keycloak-auth-provider.html)
- [docs/operations/keycloak-setup.md](../operations/keycloak-setup.md) — production realm setup runbook
- [docs/deployment/azure-deployment.md](./azure-deployment.md) — GitHub Actions repo variables and secrets (includes the Keycloak settings table)
- [docs/deployment/azure-iac-roadmap.md](./azure-iac-roadmap.md) — IaC roadmap; opt-in Keycloak module is a candidate Phase 3 item
