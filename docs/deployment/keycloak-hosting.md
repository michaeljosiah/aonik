# Hosting Keycloak in production (parked)

> **Status: parked, no current demand.** Aonik is in alpha with no production users. Hosting Keycloak adds non-trivial Azure spend (~$50–$400/month depending on tier) and meaningful operational surface for a capability that no customer has asked for. **Production Aonik deployments should currently use Auth0 or Microsoft Entra ID.** Keycloak remains fully supported as a *local-dev* identity provider — see [Identity & Access → Keycloak](https://docs.aonik.dev/operate/identity-access/keycloak) for the one-command Aspire setup.

This page exists to record why we haven't built production Keycloak infrastructure, and what we'd build if demand emerges. It is **not** a how-to for the current alpha phase.

## Context

[Spec 029](../specifications/029.keycloak-auth-provider.html) made Keycloak a first-class auth provider option on the Aonik **backend** — JWT validation, user provisioning, password reset, account service, and token exchange all work against any Keycloak realm. The accompanying [ADR-007](../decisions/007-keycloak-as-auth-provider.md) explicitly excluded Keycloak *hosting* from Aonik's scope:

> **Keycloak hosting / packaging.** Operators self-host Keycloak. Aonik does not ship a managed Keycloak. The repo's `compose.keycloak.yml` is local-dev only, not a production deployment artifact.

The local-dev story (`AONIK_AUTH_PROVIDER=Keycloak` + `pwsh .\scripts\onboard.ps1`) makes Keycloak the fastest path to a working Aonik on a laptop. The cloud story is asymmetric on purpose: production identity infrastructure is a real responsibility and Aonik shouldn't take it on speculatively.

## Why production Keycloak is parked

| Reason | Detail |
|---|---|
| **No demand** | Zero production users today, zero operator requests for hosted Keycloak. Building speculatively means the artifact rots between now and the first real user. |
| **Cost** | A minimal production Keycloak (single ACA replica + Burstable Postgres + Key Vault entries) lands at ~$50–$80/month. HA tiers push it to $300–$500/month. For an alpha with no users, this is real cost against zero validated value. |
| **Operational surface** | Backup/restore drills, realm-config versioning, Keycloak major-version upgrades, secret rotation, federation policies — every one of these is a recurring task that ties up time. None of it benefits a project with zero users. |
| **Cleaner alternatives exist** | Auth0's free tier covers small dev/staging deployments. Microsoft Entra ID is free if you're already on Microsoft 365. Both have managed identity infrastructure with zero Aonik-side ops burden. |

## What we'd build if demand emerges

When a real operator commits to running Keycloak in Azure, the design space is:

| Decision | Likely answer |
|---|---|
| **Where it lives** | Separate Bicep stack at `iac/azure/stacks/keycloak/main.bicep`, separate resource group `aonik-identity-<env>`. Not bundled into `iac/azure/stacks/aca/main.bicep`. |
| **Backing store** | Azure Database for PostgreSQL Flexible Server, Burstable B1ms default with a parameter to scale up. |
| **Realm import** | Azure Files share mounting [`infra/keycloak/realm-export.json`](../../infra/keycloak/realm-export.json); `--import-realm` fires on first start. |
| **Deploy workflow** | New `.github/workflows/cd-keycloak.yml`; operator runs it explicitly. Never auto-deployed from `cd-infra`. |
| **HA story** | Single replica + B1ms Postgres for v1. Multi-replica + zone-redundant Postgres behind a parameter. |
| **Federation, secret rotation, version upgrades** | Operator-driven (Admin Console + manual workflow). Automation belongs in follow-up specs once we know what real operators want. |

The path to unfreezing is a spec-031 proposal triggered by an operator commitment — at which point the choices above become real questions instead of hypotheticals.

## What still works today

Local development is unchanged and is the supported way to use Keycloak with Aonik right now:

- `pwsh .\scripts\onboard.ps1` — one command, Keycloak runs via Aspire on your laptop, signs you in as `admin@aonik.local`
- The backend's `Auth.Provider=Keycloak` mode works against any Keycloak instance you point it at — so if you're personally curious, you can stand up a free Keycloak Cloud trial, paste the realm URL into the GitHub repo variables, and exercise the full cloud flow without Aonik's IaC

The [CI/CD plumbing for Keycloak](./azure-deployment.md#auth0-example-individual-variables) (env-var forwarding, Dockerfile build args, validation switch) **stays in place** because it's free to keep — those workflow lines do nothing unless an operator explicitly sets `VITE_AUTH_PROVIDER=keycloak`. If demand ever emerges, the deployment surface is wired and waiting.

## What to do today

- **Local dev** — Keycloak via Aspire is the recommended fast path. No cost, no external accounts.
- **Production / staging Aonik** — use Auth0 or Microsoft Entra ID. Both have managed offerings with free dev tiers and a much smaller ops footprint than self-hosted Keycloak.
- **If you specifically want to run Keycloak in production today** — you can do it manually following the patterns in [Identity & Access → Keycloak production setup](https://docs.aonik.dev/operate/identity-access/keycloak#production-realm-setup), but Aonik does not provide IaC for it. You'd be wiring it together yourself; the env-var plumbing in [azure-deployment.md](./azure-deployment.md) is the integration point.

## See also

- [ADR-007 — Keycloak as a first-class auth provider](../decisions/007-keycloak-as-auth-provider.md) — the architectural decision that intentionally scoped out hosting
- [Spec 029 — full Keycloak specification](../specifications/029.keycloak-auth-provider.html) — the backend capability
- [docs/operations/keycloak-setup.md](../operations/keycloak-setup.md) — manual realm setup runbook (operator-driven, no IaC)
- [docs/deployment/azure-deployment.md](./azure-deployment.md) — GitHub Actions deployment surface (Keycloak env vars are documented but optional)
