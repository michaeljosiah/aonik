# Runbook: Deploy Runtime

Workflow: `.github/workflows/cd-deploy.yml`

## Purpose
Deploy ACA/AppService runtime updates using one explicit image release version.

## Inputs
- `profile`: `aca` or `appservice`
- `environment`: `dev|staging|prod`
- `resource_group` (optional override)
- `workload_name` (optional override)
- `acr_login_server` (optional override)
- `image_version` (optional; defaults to `github.sha`)
- `use_digest_references` (`true` recommended)
- `location` (optional)
- `mode`: `what-if` or `deploy`
- `skip_image_validation` (advanced/emergency only)

## Required GitHub Environment Secrets/Vars
- Secrets: `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `SQL_ADMIN_PASSWORD`, `ACS_CONNECTION_STRING`, `VERIFICATION_HASH_KEY`
- Optional secret fallback: `AZURE_CLIENT_SECRET`
- Variable: `AZURE_RESOURCE_GROUP`
- Optional variables:
  - `API_AUTH_PROVIDER`, `API_AUTH_TENANT_ROUTING`
  - `API_AUTH_AUTH0_AUTHORITY`, `API_AUTH_AUTH0_AUDIENCE`
  - `API_AUTH_AZUREAD_AUTHORITY`, `API_AUTH_AZUREAD_AUDIENCE`
  - `API_PLATFORM_ADMIN_ROLE_CLAIM_TYPE`, `API_PLATFORM_ADMIN_ROLE_VALUE`, `API_PLATFORM_ADMIN_SCOPE_CLAIM_TYPE`, `API_PLATFORM_ADMIN_ADMIN_EMAIL_0`
  - `API_BLOB_STORAGE_PROVIDER`, `API_BLOB_STORAGE_AZURE_ACCOUNT_NAME`, `API_BLOB_STORAGE_PROFILE_PHOTOS_PUBLIC_BASE_URL`, `API_BLOB_STORAGE_PRODUCT_IMAGES_PUBLIC_BASE_URL`, `API_BLOB_STORAGE_DOCUMENTS_PUBLIC_BASE_URL`
  - `WORKER_BLOB_STORAGE_PROVIDER`, `WORKER_BLOB_STORAGE_AZURE_ACCOUNT_NAME`
  - Settings (IdP): `API_SETTINGS_AUTH_PROVIDER`, `API_SETTINGS_AUTH_AUTH0_DOMAIN`, `API_SETTINGS_AUTH_AUTH0_CLIENT_ID`, `API_SETTINGS_AUTH_AUTH0_CONNECTION`, `API_SETTINGS_AUTH_AUTH0_MANAGEMENT_AUDIENCE`, `API_SETTINGS_AUTH_AUTH0_AUDIENCE`, `API_SETTINGS_AUTH_AZUREAD_AUTHORITY`, `API_SETTINGS_AUTH_AZUREAD_AUDIENCE`, `API_SETTINGS_AUTH_AZUREAD_CLIENT_ID`, `API_SETTINGS_AUTH_AZUREAD_TENANT_ID`, `API_SETTINGS_AUTH_AZUREAD_UPN_DOMAIN`
  - Communication: `API_COMMUNICATION_AZURE_EMAIL_FROM`, `API_COMMUNICATION_AZURE_SMS_FROM`
  - Bootstrap: `API_BOOTSTRAP_ENABLED`
  - Feature flags: `API_FEATURE_BILLPAYMENTS_INVOICING_CREATE`, `API_FEATURE_BILLPAYMENTS_INVOICING_ISSUE`, `API_FEATURE_BILLPAYMENTS_INVOICING_PAYMENT`, `API_FEATURE_BILLPAYMENTS_INVOICING_DISCOUNTS`, `API_FEATURE_BILLPAYMENTS_INVOICING_ALLOCATIONS`, `API_FEATURE_BILLPAYMENTS_CUSTOMER_ACCOUNTS_MANAGEMENT`
  - `API_APP_SETTINGS_JSON` (legacy bundle format)
  - `WORKER_APP_SETTINGS_JSON` (legacy bundle format)

Preferred approach is the explicit per-setting variable list above; the workflow maps those variables to the corresponding .NET configuration keys.
JSON bundle variables remain supported for backward compatibility.

## Steps
1. Set `image_version` to a known image release version.
2. Run `mode=what-if`.
3. Run `mode=deploy`.
4. Verify endpoints and telemetry.

## Fail-fast behavior
- Deployment fails if any required service image for the selected version is missing.
- Authentication/authorization or transport errors during ACR metadata queries fail fast with a separate message (not classified as missing tags).
- No automatic cross-service fallback is applied.
- Use `skip_image_validation=true` only for controlled emergency recovery.


## Optional Combined Execution

If image release is not required, run `.github/workflows/cd-pipeline.yml` with `build_images=false` and set `image_version` explicitly.
