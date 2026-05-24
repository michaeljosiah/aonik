# Azure IaC Roadmap (ACA-First)

## Objective

Establish repeatable Azure infrastructure provisioning for AONIK with an ACA-first profile, plus an App Service fallback profile.

## Phase 1 (Current)

- Define reusable Bicep modules for shared and data resources.
- Provision ACA runtime profile for API + Worker.
- Provide environment parameter templates for `dev`, `staging`, and `prod`.
- Document deployment workflow and security baseline.

## Phase 2 (Hardening)

- Private endpoints and restricted network access for SQL and Key Vault.
- Policy-as-code assignments (TLS, public access restrictions, approved SKUs).
- CI/CD deployment stages with `what-if` + approval gates.
- Database migration job/runbook integrated into release flow.

## Phase 3 (Operational Maturity)

- Backup/restore and DR runbooks tested per environment.
- SLOs and autoscaling tuning for API and Worker.
- Cost management dashboards and budget alerts.
- Progressive delivery strategy (canary/revisions) for API updates.
## Parked

- **Keycloak hosting in Azure** — Aonik is in alpha with no production users and no operator demand for hosted Keycloak. Production deployments today should use Auth0 or Microsoft Entra ID. Keycloak remains the recommended *local-dev* identity provider (one-command via Aspire). Unfreezing this requires a concrete operator commitment plus a spec-031 design pass — see [`docs/deployment/keycloak-hosting.md`](./keycloak-hosting.md) for the reasoning and the design space.
