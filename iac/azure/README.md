# AONIK Azure IaC (Bicep)

This folder provides an Azure-first Infrastructure as Code baseline for AONIK using **Azure Container Apps** (ACA) for `Aonik.Api`, `Aonik.Worker`, and `Aonik.AdminUi`.

## Layout

- `modules/common.bicep`: shared operational resources (ACR, Log Analytics, App Insights)
- `modules/data.bicep`: Azure SQL + Key Vault + connection secret + locks + diagnostics
- `modules/network.bicep`: VNet, subnets, private endpoints, private DNS zones (conditional)
- `stacks/aca/main.bicep`: Container Apps-based runtime stack
- `environments/{dev|staging|prod}/aca.parameters.json`: environment parameter templates

## AONIK Architectural Alignment

- **API + Worker are stateless runtime services** and can scale independently.
- **Ledger and financial truth remain in SQL-backed domain state** (source-of-truth is still application + ledger model).
- **Secrets are pulled from Key Vault via managed identity** to avoid embedding financial credentials in images.
- **Observability is provisioned by default** through Log Analytics and Application Insights.
- **Runtime configuration** (feature flags, communication, blob storage, platform admin, bootstrap) is managed via the **Settings module** (database-backed, editable from Admin UI) rather than GitHub environment variables.

## Prerequisites

- Azure CLI with Bicep support
- A target subscription and resource group

> For first-run environments, bootstrap infrastructure first using `.github/workflows/cd-infra.yml`; runtime images are published afterward by `CI` on pushes to `master` (or manually via `.github/workflows/cd-images.yml` if needed).

## Deploy

```bash
az deployment group create \
  --resource-group <resource-group> \
  --template-file iac/azure/stacks/aca/main.bicep \
  --parameters @iac/azure/environments/dev/aca.parameters.json
```

## Important Notes

- Replace `REPLACE_WITH_*` values in parameter files before deployment.
- `sqlAdminPassword` is included as a secure parameter placeholder; inject it from your CI secret store.

## Production Hardening

### Network Isolation (Private Endpoints)

Production ACA parameter files set `enableNetworkIsolation: true`, which:
- Creates a VNet (`10.0.0.0/16`) with two subnets: `aca` (/23, delegated) and `private-endpoints` (/24)
- Deploys private endpoints for SQL Server and Key Vault
- Creates private DNS zones (`privatelink.database.windows.net`, `privatelink.vaultcore.azure.net`) linked to the VNet
- Sets `publicNetworkAccess: 'Disabled'` on SQL Server and Key Vault
- VNet-integrates the ACA managed environment so container apps route traffic through the VNet

Dev and staging environments run with public access by default. The `enableNetworkIsolation` param defaults to `false`.

> **Important**: VNet integration on an existing ACA environment requires recreating the environment. For existing prod deployments, plan a migration window.

### Resource Locks

Production parameter files (`prod/*.parameters.json`) set `enableResourceLocks: true`, which creates `CanNotDelete` locks on SQL Server and Key Vault. This prevents accidental deletion but does not block updates or container image rollouts.

### Diagnostic Settings

When Log Analytics is provisioned (default), diagnostic settings are automatically configured for:
- **SQL Server**: AllMetrics
- **SQL Database**: SQLSecurityAuditEvents, SQLInsights, Errors, AllMetrics
- **Key Vault**: Audit logs, AllMetrics

### Drift Detection

A scheduled workflow (`.github/workflows/drift-detection.yml`) runs weekly and compares deployed state against Bicep templates using `what-if`. It can also be triggered manually for a specific environment.

## GitHub Actions CD

Use the separated workflow model:

1. `.github/workflows/cd-infra.yml` (infra-only bootstrap)
2. `.github/workflows/ci.yml` (PR build/test, plus image publish on pushes to `master`)
3. `.github/workflows/cd-deploy.yml` (runtime rollout with fail-fast image checks and approval gates)
4. `.github/workflows/cd-images.yml` (manual image-build fallback)
5. `.github/workflows/drift-detection.yml` (weekly drift checks)

Reusable composite actions:
- `.github/actions/azure-login` — OIDC + SP secret fallback authentication
- `.github/actions/bicep-deploy` — compile, what-if (with AuthorizationFailed fallback), deploy
- `.github/actions/resolve-params` — parameter file resolution for bootstrap and runtime modes

### Required GitHub environment secrets

Configure these secrets per environment (`dev`, `staging`, `prod`):

| Secret | Used by | Description |
|--------|---------|-------------|
| `SQL_ADMIN_PASSWORD` | cd-infra, cd-deploy | SQL Server admin password |
| `BOOTSTRAP_SETUP_SECRET` | cd-deploy | One-time bootstrap install code → `Bootstrap__SetupSecret` |
| `ACS_CONNECTION_STRING` | cd-infra | Azure Communication Services connection string (optional) |
| `VERIFICATION_HASH_KEY` | cd-infra | HMAC hash key for verification service (optional) |
| `AZURE_CLIENT_SECRET` | all | Service principal secret (optional; enables SP fallback over OIDC) |
| `AI__OPENAI__APIKEY` | cd-deploy | OpenAI API key for AI subsystem |
| `SETTINGS__AUTH_AUTH0_MANAGEMENTCLIENTSECRET` | cd-deploy | Auth0 Management API client secret |
| `FINANCE__PERSONALFINANCE__PLAID__SECRET` | cd-deploy | Plaid API secret key |

### Required GitHub repository variables

| Variable | Used by | Description |
|----------|---------|-------------|
| `AZURE_CLIENT_ID` | ci, cd-infra, cd-deploy, drift-detection, cd-images | Azure AD application (client) ID |
| `AZURE_TENANT_ID` | ci, cd-infra, cd-deploy, drift-detection, cd-images | Azure AD directory (tenant) ID |
| `AZURE_SUBSCRIPTION_ID` | ci, cd-infra, cd-deploy, drift-detection, cd-images | Azure subscription ID |
| `WORKLOAD_NAME` | ci, cd-infra, cd-deploy, cd-images | Workload name for naming convention and ACR derivation |
| `VITE_AUTH_PROVIDER` | ci, cd-images | Admin UI auth provider (`azure-ad`, `auth0`, `mock`) |
| `VITE_API_BASE_URL` | ci, cd-images | Admin UI API base URL |
| `VITE_AZURE_AD_CLIENT_ID` | ci, cd-images | Azure AD client ID (if `azure-ad`) |
| `VITE_AZURE_AD_TENANT_ID` | ci, cd-images | Azure AD tenant ID (if `azure-ad`) |
| `VITE_AZURE_AD_REDIRECT_URI` | ci, cd-images | Azure AD redirect URI (optional) |
| `VITE_AZURE_AD_API_SCOPE` | ci, cd-images | Azure AD API scope (optional) |
| `VITE_AUTH0_DOMAIN` | ci, cd-images | Auth0 domain (if `auth0`) |
| `VITE_AUTH0_CLIENT_ID` | ci, cd-images | Auth0 client ID (if `auth0`) |
| `VITE_AUTH0_REDIRECT_URI` | ci, cd-images | Auth0 redirect URI (optional) |
| `VITE_AUTH0_AUDIENCE` | ci, cd-images | Auth0 audience (optional) |

### Required GitHub environment variables

| Variable | Used by | Description |
|----------|---------|-------------|
| `AZURE_RESOURCE_GROUP` | cd-infra, cd-deploy, drift-detection | Target Azure resource group |

### Optional API/Worker runtime configuration

Runtime app settings for API and Worker containers are defined as **individual** GitHub Environment variables or secrets. The deploy workflow collects any env var matching a recognised prefix and forwards it to the container.

| Prefix | Target | Example |
|--------|--------|---------|
| `AI__` | API | `AI__PROVIDER`, `AI__OPENAI__MODEL` |
| `SETTINGS__` | API | `SETTINGS__AUTH_PROVIDER`, `SETTINGS__AUTH_AUTH0_DOMAIN` |
| `FINANCE__` | API | `FINANCE__PERSONALFINANCE__PLAID__CLIENTID` |
| `WORKER__` | Worker | (reserved for future Worker-specific settings) |

See `docs/runbooks/deploy-runtime.md` for the full variable list.

### Settings managed via the database (not GitHub variables)

The following settings are registered in `SettingDefinitions` and seeded as Global-scope defaults on startup. They can be edited at runtime via the Admin UI or Settings API without redeployment:

- **Communication**: email from address, SMS from phone number
- **Blob Storage**: provider, Azure account name, public base URLs for profile photos / product images / documents
- **Platform Admin**: role claim type, role value, scope claim type, admin emails
- **Bootstrap**: enabled flag
- **Feature Flags**: all `BillPayments.*` feature flags
- **Auth (IdP management)**: all Auth0/AzureAd settings for the Settings module cascade

### Quick run checklist

1. Ensure GitHub environment secrets are set for the selected environment.
2. Run **CD: Infrastructure** (`what-if` then `deploy`).
3. Merge or push the desired commit to `master` and wait for **CI** to publish images.
4. Record `image-release-manifest.json` from the `image-release-<sha>` artifact if needed.
5. Run **CD: Deploy** with the same release version (`what-if` then `deploy`).

### Workflow behavior

- ACA is the only deployment target.
- Uses `what-if` preview mode before deployment for bootstrap/runtime workflows.
- Uses Azure login via OIDC (`azure/login`) by default, with service principal secret fallback (`AZURE_CLIENT_SECRET`) when required.
- Runtime deploy enforces one cohesive image version across required services and blocks mixed/incomplete releases.

For full click-by-click GitHub setup (OIDC, environments, secrets, workflow inputs, and validation), see `docs/deployment/azure-deployment.md`.

For rollback procedures, see `docs/runbooks/rollback.md`.
