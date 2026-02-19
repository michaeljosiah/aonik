# AONIK Azure IaC (Bicep)

This folder provides an Azure-first Infrastructure as Code baseline for AONIK with two deployment profiles:

- **ACA profile (primary)**: Azure Container Apps for `Aonik.Api`, `Aonik.Worker`, and `Aonik.AdminUi`
- **App Service profile (fallback)**: App Service for `Aonik.Api` and `Aonik.AdminUi`

AKS is intentionally excluded for now.

## Layout

- `modules/common.bicep`: shared operational resources (ACR, Log Analytics, App Insights)
- `modules/data.bicep`: Azure SQL + Key Vault + connection secret
- `profiles/aca/main.bicep`: Container Apps-based runtime profile
- `profiles/appservice/main.bicep`: App Service-based runtime profile
- `environments/{dev|staging|prod}/*.parameters.json`: environment parameter templates

## AONIK Architectural Alignment

- **API + Worker are stateless runtime services** and can scale independently.
- **Ledger and financial truth remain in SQL-backed domain state** (source-of-truth is still application + ledger model).
- **Secrets are pulled from Key Vault via managed identity** to avoid embedding financial credentials in images.
- **Observability is provisioned by default** through Log Analytics and Application Insights.

## Prerequisites

- Azure CLI with Bicep support
- A target subscription and resource group

> For first-run environments, bootstrap infrastructure first using `.github/workflows/azure-platform-bootstrap.yml`; runtime images are published afterward via `.github/workflows/azure-image-release.yml`.

## Deploy ACA profile

```bash
az deployment group create \
  --resource-group <resource-group> \
  --template-file infra/profiles/aca/main.bicep \
  --parameters @infra/environments/dev/aca.parameters.json
```

## Deploy App Service profile

```bash
az deployment group create \
  --resource-group <resource-group> \
  --template-file infra/profiles/appservice/main.bicep \
  --parameters @infra/environments/dev/appservice.parameters.json
```

## Important Notes

- Replace `REPLACE_WITH_*` values in parameter files before deployment.
- `sqlAdminPassword` is included as a secure parameter placeholder; inject it from your CI secret store.
- For production hardening, move SQL/Key Vault to private endpoints and lock public network access.

## GitHub Actions CD (Recommended)

Use the separated workflow model:

1. `.github/workflows/azure-platform-bootstrap.yml` (infra-only)
2. `.github/workflows/azure-image-release.yml` (build/tag/push API/Worker/AdminUI images)
3. `.github/workflows/azure-runtime-deploy.yml` (runtime rollout with fail-fast image checks)

Legacy `.github/workflows/azure-iac-cd.yml` remains available temporarily during migration.

### Required GitHub environment secrets

Configure these secrets per environment (`dev`, `staging`, `prod`):

- `AZURE_CLIENT_ID`
- `AZURE_TENANT_ID`
- `AZURE_SUBSCRIPTION_ID`
- `AZURE_CLIENT_SECRET` (optional; if set, workflow uses secret-based service principal auth instead of OIDC)
- `SQL_ADMIN_PASSWORD`

### Quick run checklist

1. Ensure GitHub environment secrets are set for the selected environment.
2. Run **Azure Platform Bootstrap** (`what-if` then `deploy`).
3. Run **Azure Image Release** and record `image-release-manifest.json`.
4. Run **Azure Runtime Deploy** with the same release version (`what-if` then `deploy`).

### Workflow behavior

- Supports both `aca` and `appservice` profiles.
- Uses `what-if` preview mode before deployment for bootstrap/runtime workflows.
- Uses Azure login via OIDC (`azure/login`) by default, with service principal secret fallback (`AZURE_CLIENT_SECRET`) when required.
- Runtime deploy enforces one cohesive image version across required services and blocks mixed/incomplete releases.

For full click-by-click GitHub setup (OIDC, environments, secrets, workflow inputs, and validation), see `docs/deployment/azure-deployment.md`.
