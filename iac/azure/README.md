# AONIK Azure IaC (Bicep)

This folder provides an Azure-first Infrastructure as Code baseline for AONIK with two deployment profiles:

- **ACA profile (primary)**: Azure Container Apps for `Aonik.Api`, `Aonik.Worker`, and `Aonik.AdminUi`
- **App Service profile (fallback)**: App Service for `Aonik.Api` and `Aonik.AdminUi`

AKS is intentionally excluded for now.

## Layout

- `modules/common.bicep`: shared operational resources (ACR, Log Analytics, App Insights)
- `modules/data.bicep`: Azure SQL + Key Vault + connection secret + locks + diagnostics
- `modules/network.bicep`: VNet, subnets, private endpoints, private DNS zones (conditional)
- `stacks/aca/main.bicep`: Container Apps-based runtime stack
- `stacks/appservice/main.bicep`: App Service-based runtime stack
- `environments/{dev|staging|prod}/*.parameters.json`: environment parameter templates

## AONIK Architectural Alignment

- **API + Worker are stateless runtime services** and can scale independently.
- **Ledger and financial truth remain in SQL-backed domain state** (source-of-truth is still application + ledger model).
- **Secrets are pulled from Key Vault via managed identity** to avoid embedding financial credentials in images.
- **Observability is provisioned by default** through Log Analytics and Application Insights.

## Prerequisites

- Azure CLI with Bicep support
- A target subscription and resource group

> For first-run environments, bootstrap infrastructure first using `.github/workflows/cd-infra.yml`; runtime images are published afterward via `.github/workflows/cd-images.yml`.

## Deploy ACA profile

```bash
az deployment group create \
  --resource-group <resource-group> \
--template-file iac/azure/stacks/aca/main.bicep \
  --parameters @iac/azure/environments/dev/aca.parameters.json
```

## Deploy App Service profile

```bash
az deployment group create \
  --resource-group <resource-group> \
--template-file iac/azure/stacks/appservice/main.bicep \
  --parameters @iac/azure/environments/dev/appservice.parameters.json
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

## GitHub Actions CD (Recommended)

Use the separated workflow model:

1. `.github/workflows/cd-infra.yml` (infra-only)
2. `.github/workflows/cd-images.yml` (build/tag/push API/Worker/AdminUI images)
3. `.github/workflows/cd-deploy.yml` (runtime rollout with fail-fast image checks)
4. `.github/workflows/drift-detection.yml` (weekly drift checks)



### Required GitHub environment secrets

Configure these secrets per environment (`dev`, `staging`, `prod`):

- `AZURE_CLIENT_ID`
- `AZURE_TENANT_ID`
- `AZURE_SUBSCRIPTION_ID`
- `AZURE_CLIENT_SECRET` (optional; if set, workflow uses secret-based service principal auth instead of OIDC)
- `SQL_ADMIN_PASSWORD`
- `ACS_CONNECTION_STRING` (Azure Communication Services connection string; stored in Key Vault)
- `VERIFICATION_HASH_KEY` (HMAC hash key for verification service; stored in Key Vault)

### Required GitHub environment variables

Set these per environment (`dev`, `staging`, `prod`) so the Admin UI image is built with the correct public auth/API endpoints:

- `VITE_AUTH_PROVIDER` (`azure-ad`, `auth0`, or `mock`)
- `VITE_API_BASE_URL`

If `VITE_AUTH_PROVIDER` is unset, image release defaults it to `azure-ad` during Admin UI build.

If `VITE_AUTH_PROVIDER=azure-ad`:

- `VITE_AZURE_AD_CLIENT_ID`
- `VITE_AZURE_AD_TENANT_ID`

If `VITE_AUTH_PROVIDER=auth0`:

- `VITE_AUTH0_DOMAIN`
- `VITE_AUTH0_CLIENT_ID`

Optional provider-specific overrides:

- `VITE_AZURE_AD_REDIRECT_URI`
- `VITE_AZURE_AD_API_SCOPE`
- `VITE_AUTH0_REDIRECT_URI`
- `VITE_AUTH0_AUDIENCE`

### Optional API/Worker runtime configuration variables

If you need to override API/Worker configuration from `appsettings` per environment without rebuilding images, set these explicit GitHub environment variables (mapped by deploy workflow to .NET keys):

- `API_AUTH_PROVIDER` → `Auth__Provider`
- `API_AUTH_TENANT_ROUTING` → `Auth__TenantRouting`
- `API_AUTH_AUTH0_AUTHORITY` → `Auth__Auth0__Authority`
- `API_AUTH_AUTH0_AUDIENCE` → `Auth__Auth0__Audience`
- `API_AUTH_AZUREAD_AUTHORITY` → `Auth__AzureAd__Authority`
- `API_AUTH_AZUREAD_AUDIENCE` → `Auth__AzureAd__Audience`
- `API_PLATFORM_ADMIN_ROLE_CLAIM_TYPE` → `PlatformAdmin__RoleClaimType`
- `API_PLATFORM_ADMIN_ROLE_VALUE` → `PlatformAdmin__RoleValue`
- `API_PLATFORM_ADMIN_SCOPE_CLAIM_TYPE` → `PlatformAdmin__ScopeClaimType`
- `API_PLATFORM_ADMIN_ADMIN_EMAIL_0` → `PlatformAdmin__AdminEmails__0`
- `API_BLOB_STORAGE_PROVIDER` → `BlobStorage__Provider`
- `API_BLOB_STORAGE_AZURE_ACCOUNT_NAME` → `BlobStorage__Azure__AccountName`
- `API_BLOB_STORAGE_PROFILE_PHOTOS_PUBLIC_BASE_URL` → `BlobStorage__ProfilePhotos__PublicBaseUrl`
- `API_BLOB_STORAGE_PRODUCT_IMAGES_PUBLIC_BASE_URL` → `BlobStorage__ProductImages__PublicBaseUrl`
- `API_BLOB_STORAGE_DOCUMENTS_PUBLIC_BASE_URL` → `BlobStorage__Documents__PublicBaseUrl`
- `WORKER_BLOB_STORAGE_PROVIDER` → `BlobStorage__Provider`
- `WORKER_BLOB_STORAGE_AZURE_ACCOUNT_NAME` → `BlobStorage__Azure__AccountName`

#### Settings (IdP Management API)

The `Settings` section uses flat dot-notation keys. As environment variables, `__` replaces `:` but dots in key names are literal:

- `API_SETTINGS_AUTH_PROVIDER` → `Settings__Auth.Provider`
- `API_SETTINGS_AUTH_AUTH0_DOMAIN` → `Settings__Auth.Auth0.Domain`
- `API_SETTINGS_AUTH_AUTH0_CLIENT_ID` → `Settings__Auth.Auth0.ClientId`
- `API_SETTINGS_AUTH_AUTH0_CONNECTION` → `Settings__Auth.Auth0.Connection`
- `API_SETTINGS_AUTH_AUTH0_MANAGEMENT_AUDIENCE` → `Settings__Auth.Auth0.ManagementAudience`
- `API_SETTINGS_AUTH_AUTH0_AUDIENCE` → `Settings__Auth.Auth0.Audience`
- `API_SETTINGS_AUTH_AZUREAD_AUTHORITY` → `Settings__Auth.AzureAd.Authority`
- `API_SETTINGS_AUTH_AZUREAD_AUDIENCE` → `Settings__Auth.AzureAd.Audience`
- `API_SETTINGS_AUTH_AZUREAD_CLIENT_ID` → `Settings__Auth.AzureAd.ClientId`
- `API_SETTINGS_AUTH_AZUREAD_TENANT_ID` → `Settings__Auth.AzureAd.TenantId`
- `API_SETTINGS_AUTH_AZUREAD_UPN_DOMAIN` → `Settings__Auth.AzureAd.UserPrincipalNameDomain`

#### Communication

The ACS connection string is a **secret** (stored in Key Vault via `ACS_CONNECTION_STRING`). The remaining fields are variables:

- `API_COMMUNICATION_AZURE_EMAIL_FROM` → `Communication__Azure__Email__FromAddress`
- `API_COMMUNICATION_AZURE_SMS_FROM` → `Communication__Azure__Sms__FromPhoneNumber`

#### Bootstrap

- `API_BOOTSTRAP_ENABLED` → `Bootstrap__Enabled` (defaults to `false`; set to `true` only for first-run dev bootstrap)

#### Feature Management

Feature flag names contain dots (literal in environment variable values):

- `API_FEATURE_BILLPAYMENTS_INVOICING_CREATE` → `FeatureManagement__BillPayments.Invoicing.Create`
- `API_FEATURE_BILLPAYMENTS_INVOICING_ISSUE` → `FeatureManagement__BillPayments.Invoicing.Issue`
- `API_FEATURE_BILLPAYMENTS_INVOICING_PAYMENT` → `FeatureManagement__BillPayments.Invoicing.Payment`
- `API_FEATURE_BILLPAYMENTS_INVOICING_DISCOUNTS` → `FeatureManagement__BillPayments.Invoicing.Discounts`
- `API_FEATURE_BILLPAYMENTS_INVOICING_ALLOCATIONS` → `FeatureManagement__BillPayments.Invoicing.Allocations`
- `API_FEATURE_BILLPAYMENTS_CUSTOMER_ACCOUNTS_MANAGEMENT` → `FeatureManagement__BillPayments.CustomerAccounts.Management`

Backward-compatible JSON bundle variables are still supported:

- `API_APP_SETTINGS_JSON`
- `WORKER_APP_SETTINGS_JSON`

If both explicit variables and JSON bundles are present, explicit variables override duplicate keys.

### Quick run checklist

1. Ensure GitHub environment secrets are set for the selected environment.
2. Run **CD: Infrastructure** (`what-if` then `deploy`).
3. Run **CD: Container Images** and record `image-release-manifest.json`.
4. Run **CD: Deploy** with the same release version (`what-if` then `deploy`).

### Workflow behavior

- Supports both `aca` and `appservice` profiles.
- Uses `what-if` preview mode before deployment for bootstrap/runtime workflows.
- Uses Azure login via OIDC (`azure/login`) by default, with service principal secret fallback (`AZURE_CLIENT_SECRET`) when required.
- Runtime deploy enforces one cohesive image version across required services and blocks mixed/incomplete releases.

For full click-by-click GitHub setup (OIDC, environments, secrets, workflow inputs, and validation), see `docs/deployment/azure-deployment.md`.

For rollback procedures, see `docs/runbooks/rollback.md`.
