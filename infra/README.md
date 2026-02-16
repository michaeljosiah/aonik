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
- Container images for:
  - `aonik-api`
  - `aonik-worker` (ACA profile)
  - `aonik-adminui`

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

Use `.github/workflows/azure-iac-cd.yml` to run controlled IaC rollout via manual dispatch.

### Required GitHub environment secrets

Configure these secrets per environment (`dev`, `staging`, `prod`):

- `AZURE_CLIENT_ID`
- `AZURE_TENANT_ID`
- `AZURE_SUBSCRIPTION_ID`
- `SQL_ADMIN_PASSWORD`

### Quick run checklist

1. Update image tags in `infra/environments/<env>/*.parameters.json` (`apiImage`, `workerImage` for ACA, and `adminUiImage`).
2. Ensure GitHub environment secrets are set for the selected environment.
3. Run **Actions** → **Azure IaC CD** with `mode=what-if` and review changes.
4. Re-run with `mode=deploy` to apply changes.

### Workflow behavior

- Supports both `aca` and `appservice` profiles.
- Supports `what-if` preview mode before deployment.
- Uses Azure OIDC login (`azure/login`) instead of long-lived service principal passwords.

For full click-by-click GitHub setup (OIDC, environments, secrets, workflow inputs, and validation), see `docs/deployment/azure-deployment.md`.
